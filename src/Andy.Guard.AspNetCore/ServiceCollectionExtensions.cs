using Andy.Guard.InputScanners;
using Andy.Guard.InputScanners.Abstractions;
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
    /// Registers the default scanner set and the registry in the container.
    /// </summary>
    public static IServiceCollection AddGuardScanning(this IServiceCollection services)
    {
        // Base scanner (currently stub)
        services.AddSingleton<IPromptInjectionScanner, PromptInjectionScanner>();

        // Generic adapters and registry
        services.AddSingleton<ITextScanner, PromptInjectionTextScanner>();
        services.AddSingleton<IScannerRegistry, ScannerRegistry>();
        return services;
    }
}

