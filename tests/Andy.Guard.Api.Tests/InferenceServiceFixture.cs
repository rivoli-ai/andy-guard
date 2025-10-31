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

    private readonly INetwork _network;
    private readonly IVolume _modelDataVolume;
    private readonly IContainer _modelAssetsContainer;
    private readonly IContainer _tokenizerContainer;
    private readonly IContainer _inferenceContainer;

    private IHost? _host;
    private bool _disposed;
    private string? _skipReason;

    public string AndyInferenceBaseUrl { get; private set; } = string.Empty;
    public bool IsAvailable => _skipReason is null;
    public string? SkipReason => _skipReason;

    public InferenceServiceFixture()
    {
        _network = new NetworkBuilder()
            .WithName($"andy-guard-tests-{Guid.NewGuid():N}")
            .Build();

        var configDirectory = ResolveConfigDirectory();

        _modelDataVolume = new VolumeBuilder()
            .WithName($"andy-guard-model-data-{Guid.NewGuid():N}")
            .Build();

        _modelAssetsContainer = new ContainerBuilder()
            .WithImage(ModelAssetsImage)
            .WithName($"andy-guard-model-assets-{Guid.NewGuid():N}")
            .WithNetwork(_network)
            .WithVolumeMount(_modelDataVolume, "/models", AccessMode.ReadWrite)
            .WithCommand("/bin/sh", "-c", "while true; do sleep 3600; done")
            .Build();

        _tokenizerContainer = new ContainerBuilder()
            .WithImage(TokenizerImage)
            .WithName($"andy-guard-tokenizer-{Guid.NewGuid():N}")
            .WithNetwork(_network)
            .WithNetworkAliases("tokenizer-service")
            .WithPortBinding(TokenizerPort, true)
            .WithBindMount(configDirectory, "/app/config", AccessMode.ReadOnly)
            .WithWaitStrategy(
                Wait.ForUnixContainer()
                    .UntilHttpRequestIsSucceeded(r => r
                        .ForPort(TokenizerPort)
                        .ForPath("/health")
                        .ForStatusCode(HttpStatusCode.OK)))
            .Build();

        _inferenceContainer = new ContainerBuilder()
            .WithImage(InferenceImage)
            .WithName($"andy-guard-inference-{Guid.NewGuid():N}")
            .WithNetwork(_network)
            .WithPortBinding(InferencePort, true)
            .WithEnvironment("TokenizerServiceUrl", "http://tokenizer-service:8000")
            .WithEnvironment("ModelConfiguration__TokenizerServiceUrl", "http://tokenizer-service:8000")
            .WithEnvironment("ModelsConfigPath", "/app/config/models.json")
            .WithBindMount(configDirectory, "/app/config", AccessMode.ReadOnly)
            .WithVolumeMount(_modelDataVolume, "/app/models", AccessMode.ReadOnly)
            .WithWaitStrategy(
                Wait.ForUnixContainer()
                    .UntilHttpRequestIsSucceeded(r => r
                        .ForPort(InferencePort)
                        .ForPath("/health")
                        .ForStatusCode(HttpStatusCode.OK)))
            .Build();
    }

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
        try
        {
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
            _skipReason = "Docker bridge network plugin is unavailable; integration tests require bridge networking.";
            AndyInferenceBaseUrl = string.Empty;
            _host = null;
            return;
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
        await _network.DeleteAsync();
        await _modelDataVolume.DeleteAsync();
        _host?.Dispose();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _host?.Dispose();
    }

    public void SkipIfUnavailable()
    {
        if (_skipReason is not null)
        {
            Assert.Skip(_skipReason);
        }
    }

    private static async Task StopContainerAsync(IContainer container)
    {
        try
        { await container.StopAsync(); }
        catch { }
        await container.DisposeAsync();
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

    private async Task CleanupAfterFailureAsync()
    {
        await IgnoreFailureAsync(() => StopContainerAsync(_inferenceContainer));
        await IgnoreFailureAsync(() => StopContainerAsync(_tokenizerContainer));
        await IgnoreFailureAsync(() => StopContainerAsync(_modelAssetsContainer));
        await IgnoreFailureAsync(() => _network.DeleteAsync());
        await IgnoreFailureAsync(() => _modelDataVolume.DeleteAsync());
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
