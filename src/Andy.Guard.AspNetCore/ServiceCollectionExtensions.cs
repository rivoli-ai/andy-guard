using Andy.Guard.InputScanners;
using Andy.Guard.AspNetCore.Options;
using Andy.Guard.Scanning;
using Andy.Guard.Scanning.Abstractions;
using Microsoft.Extensions.DependencyInjection;

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
        // Options support for middleware configuration via IOptions<PromptScanningOptions>
        services.AddOptions<PromptScanningOptions>();

        services.AddSingleton<IInputScanner, PromptInjectionScanner>();
        // Add other input scanners here
        // e.g., services.AddSingleton<IInputScanner, PiiScanner>();

        // Generic adapters and registry
        services.AddSingleton<IInputScannerRegistry, InputScannerRegistry>();

        return services;
    }

    /// <summary>
    /// Registers the default model output scanner registry in the container.
    /// </summary>
    public static IServiceCollection AddModelOutputScanning(this IServiceCollection services)
    {
        services.AddOptions<ModelOutputScanningOptions>();
        // Add other output scanners here
        // e.g., services.AddSingleton<IOutputScanner, ToxicityScanner>();

        // Generic registry for output scanners
        services.AddSingleton<IOutputScannerRegistry, OutputScannerRegistry>();

        return services;
    }
}
