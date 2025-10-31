using System.Net;
using Andy.Guard.InputScanners;
using Docker.DotNet;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Volumes;
using Andy.Guard.Api.Extensions;
using Microsoft.Extensions.Configuration;
using Andy.Guard.Scanning.Abstractions;
using Xunit;

namespace Andy.Guard.Tests;

public sealed class InferenceServiceFixture : IAsyncLifetime, IDisposable
{
    private const int TokenizerPort = 8000;
    private const int InferencePort = 8080;
    private const string TokenizerImage = "ghcr.io/rivoli-ai/andy-tokenizer-service:sha-3800b7e";
    private const string InferenceImage = "ghcr.io/rivoli-ai/andy-inference-service:sha-3800b7e";
    private const string ModelAssetsImage = "ghcr.io/rivoli-ai/andy-model-assets:v1";

    private INetwork? _network;
    private IVolume? _modelDataVolume;
    private IContainer? _modelAssetsContainer;
    private IContainer? _tokenizerContainer;
    private IContainer? _inferenceContainer;

    private IHost? _host;
    private bool _disposed;
    private string? _skipReason;

    public string AndyInferenceBaseUrl { get; private set; } = string.Empty;
    public bool IsAvailable => _skipReason is null;
    public string? SkipReason => _skipReason;

    public InferenceServiceFixture()
    {
        var configDirectory = ResolveConfigDirectory();
        ConfigurationDirectory = configDirectory;
    }

    private string ConfigurationDirectory { get; }

    public PromptInjectionScanner Scanner
    {
        get
        {
            SkipIfUnavailable();
            return _host?.Services
                .GetRequiredService<IEnumerable<IInputScanner>>()
                .OfType<PromptInjectionScanner>()
                .Single()
                ?? throw new InvalidOperationException("Host not initialized.");
        }
    }

    public async ValueTask InitializeAsync()
    {
        if (_skipReason is not null)
        {
            return;
        }

        try
        {
            _network ??= new NetworkBuilder()
                .WithName($"andy-guard-tests-{Guid.NewGuid():N}")
                .Build();

            _modelDataVolume ??= new VolumeBuilder()
                .WithName($"andy-guard-model-data-{Guid.NewGuid():N}")
                .Build();

            _modelAssetsContainer ??= new ContainerBuilder()
                .WithImage(ModelAssetsImage)
                .WithName($"andy-guard-model-assets-{Guid.NewGuid():N}")
                .WithNetwork(_network)
                .WithVolumeMount(_modelDataVolume, "/models", AccessMode.ReadWrite)
                .WithCommand("/bin/sh", "-c", "while true; do sleep 3600; done")
                .Build();

            _tokenizerContainer ??= new ContainerBuilder()
                .WithImage(TokenizerImage)
                .WithName($"andy-guard-tokenizer-{Guid.NewGuid():N}")
                .WithNetwork(_network)
                .WithNetworkAliases("tokenizer-service")
                .WithPortBinding(TokenizerPort, true)
                .WithBindMount(ConfigurationDirectory, "/app/config", AccessMode.ReadOnly)
                .WithWaitStrategy(
                    Wait.ForUnixContainer()
                        .UntilHttpRequestIsSucceeded(r => r
                            .ForPort(TokenizerPort)
                            .ForPath("/health")
                            .ForStatusCode(HttpStatusCode.OK)))
                .Build();

            _inferenceContainer ??= new ContainerBuilder()
                .WithImage(InferenceImage)
                .WithName($"andy-guard-inference-{Guid.NewGuid():N}")
                .WithNetwork(_network)
                .WithPortBinding(InferencePort, true)
                .WithEnvironment("TokenizerServiceUrl", "http://tokenizer-service:8000")
                .WithEnvironment("ModelConfiguration__TokenizerServiceUrl", "http://tokenizer-service:8000")
                .WithEnvironment("ModelsConfigPath", "/app/config/models.json")
                .WithBindMount(ConfigurationDirectory, "/app/config", AccessMode.ReadOnly)
                .WithVolumeMount(_modelDataVolume, "/app/models", AccessMode.ReadOnly)
                .WithWaitStrategy(
                    Wait.ForUnixContainer()
                        .UntilHttpRequestIsSucceeded(r => r
                            .ForPort(InferencePort)
                            .ForPath("/health")
                            .ForStatusCode(HttpStatusCode.OK)))
                .Build();

            await _network.CreateAsync();
            await _modelDataVolume.CreateAsync();
            await _modelAssetsContainer.StartAsync();
            await _tokenizerContainer.StartAsync();
            await _inferenceContainer.StartAsync();
            var dynamicHostPort = _inferenceContainer.GetMappedPublicPort(InferencePort);
            AndyInferenceBaseUrl = $"http://localhost:{dynamicHostPort}/api";
            var overrideConfig = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DownstreamApis:AndyInference:BaseUrl"] = AndyInferenceBaseUrl,
                })
                .Build();

            _host = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.AddConfiguration(overrideConfig);
                })
                .ConfigureServices((context, services) =>
                {
                    services.AddApplicationServices(context.Configuration);
                })
                .Build();

            await _host.StartAsync();
            _skipReason = null;
        }
        catch (DockerApiException ex) when (IsBridgePluginMissing(ex))
        {
            await CleanupAfterFailureAsync();
            SetSkip("Docker bridge network plugin is unavailable; integration tests require bridge networking.");
        }
        catch (Exception ex) when (IsDockerUnavailable(ex))
        {
            await CleanupAfterFailureAsync();
            SetSkip("Docker is unavailable or misconfigured; integration tests require Docker engine access.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!IsAvailable)
        {
            await CleanupAfterFailureAsync();
            _host?.Dispose();
            return;
        }

        if (_host != null)
            await _host.StopAsync();

        await StopContainerAsync(_inferenceContainer);
        await StopContainerAsync(_tokenizerContainer);
        await StopContainerAsync(_modelAssetsContainer);
        await DeleteNetworkAsync(_network);
        await DeleteVolumeAsync(_modelDataVolume);
        _host?.Dispose();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _host?.Dispose();
        _network = null;
        _modelDataVolume = null;
        _modelAssetsContainer = null;
        _tokenizerContainer = null;
        _inferenceContainer = null;
    }

    public void SkipIfUnavailable()
    {
        if (_skipReason is not null)
        {
            Assert.Skip(_skipReason);
        }
    }

    private void SetSkip(string reason)
    {
        _skipReason = reason;
        AndyInferenceBaseUrl = string.Empty;
        _host?.Dispose();
        _host = null;
    }

    private static async Task StopContainerAsync(IContainer? container)
    {
        if (container is null)
            return;
        try
        { await container.StopAsync(); }
        catch { }
        await container.DisposeAsync();
    }

    private static async Task DeleteNetworkAsync(INetwork? network)
    {
        if (network is null)
            return;
        try
        { await network.DeleteAsync(); }
        catch { }
        await network.DisposeAsync();
    }

    private static async Task DeleteVolumeAsync(IVolume? volume)
    {
        if (volume is null)
            return;
        try
        { await volume.DeleteAsync(); }
        catch { }
        await volume.DisposeAsync();
    }

    private static bool IsBridgePluginMissing(DockerApiException exception)
    {
        if (exception.StatusCode != HttpStatusCode.NotFound)
            return false;

        var message = exception.ResponseBody ?? exception.Message;
        if (string.IsNullOrWhiteSpace(message))
            return false;

        return message.IndexOf("plugin", StringComparison.OrdinalIgnoreCase) >= 0
            && message.IndexOf("bridge", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsDockerUnavailable(Exception exception)
    {
        switch (exception)
        {
            case ArgumentException argEx when string.Equals(argEx.ParamName, "DockerEndpointAuthConfig", StringComparison.Ordinal):
                return true;
            case DockerApiException dockerEx when dockerEx.StatusCode == HttpStatusCode.InternalServerError:
                return true;
        }

        var message = exception.Message;
        if (string.IsNullOrWhiteSpace(message))
            return false;

        return message.IndexOf("docker is either not running or misconfigured", StringComparison.OrdinalIgnoreCase) >= 0
            || message.IndexOf("DockerEndpointAuthConfig", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private async Task CleanupAfterFailureAsync()
    {
        await IgnoreFailureAsync(() => StopContainerAsync(_inferenceContainer));
        await IgnoreFailureAsync(() => StopContainerAsync(_tokenizerContainer));
        await IgnoreFailureAsync(() => StopContainerAsync(_modelAssetsContainer));
        await IgnoreFailureAsync(() => DeleteNetworkAsync(_network));
        await IgnoreFailureAsync(() => DeleteVolumeAsync(_modelDataVolume));
        _network = null;
        _modelDataVolume = null;
        _modelAssetsContainer = null;
        _tokenizerContainer = null;
        _inferenceContainer = null;
    }

    private static async Task IgnoreFailureAsync(Func<Task> cleanup)
    {
        try
        {
            await cleanup();
        }
        catch
        {
        }
    }

    private static string ResolveConfigDirectory()
    {
        var configured = Environment.GetEnvironmentVariable("ANDY_INFERENCE_MODELS_CONFIG_PATH");
        if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured))
            return configured;

        var defaultPath = Path.Combine(AppContext.BaseDirectory, "TestData", "inference-config");
        if (Directory.Exists(defaultPath))
            return defaultPath;

        throw new DirectoryNotFoundException("Could not locate configuration directory for inference test containers.");
    }
}
