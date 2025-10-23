using Andy.Guard.InputScanners;
using Andy.Guard.AspNetCore.Options;
using Andy.Guard.Scanning;
using Andy.Guard.Scanning.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Identity.Abstractions;

namespace Andy.Guard.AspNetCore;

/// <summary>
/// Registration helpers for Guard scanners and registry.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the default input scanner set and the registry in the container.
    /// </summary>
    public static IServiceCollection AddPromptScanning(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddGuardCoreServices();

        // Options support for middleware configuration via IOptions<PromptScanningOptions>
        services.AddOptions<PromptScanningOptions>();
        services.AddScoped<IInputScannerRegistry, InputScannerRegistry>();

        services.AddSingleton<IPromptInjectionResultMapper, PromptInjectionResultMapper>();
        services.AddScoped<IInputScanner, PromptInjectionScanner>();
        // Add other input scanners here
        // e.g., services.AddSingleton<IInputScanner, PiiScanner>();

        // Generic adapters and registry

        return services;
    }

    /// <summary>
    /// Registers the default model output scanner registry in the container.
    /// </summary>
    public static IServiceCollection AddModelOutputScanning(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddGuardCoreServices();

        services.AddOptions<ModelOutputScanningOptions>();
        // Add other output scanners here
        // e.g., services.AddSingleton<IOutputScanner, ToxicityScanner>();

        // Generic registry for output scanners
        services.AddSingleton<IOutputScannerRegistry, OutputScannerRegistry>();

        return services;
    }

    private static IServiceCollection AddGuardCoreServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IAuthorizationHeaderProvider, NoopAuthorizationHeaderProvider>();
        services.AddScoped<InferenceApiClient>();

        return services;
    }
}
