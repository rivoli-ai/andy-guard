using Andy.Guard.Api.Models;
using Andy.Guard.Api.Services.Abstractions;
using Andy.Guard.InputScanners;
using Andy.Guard.InputScanners.Abstractions;

namespace Andy.Guard.Api.Services;

/// <summary>
/// Adapter that exposes the library's <see cref="IPromptInjectionScanner"/> as a generic <see cref="ITextScanner"/>.
/// </summary>
internal sealed class PromptInjectionTextScanner : ITextScanner
{
    private readonly IPromptInjectionScanner _scanner;

    public PromptInjectionTextScanner(IPromptInjectionScanner scanner)
    {
        _scanner = scanner;
    }

    public string Name => "prompt_injection";

    public bool SupportsTarget(ScanTarget target) => true;

    public async Task<ScanResult> ScanAsync(ScanTarget target, string text, ScanOptions? options = null)
    {
        return target == ScanTarget.Input
            ? await _scanner.ScanInputAsync(text, options)
            : await _scanner.ScanOutputAsync(text, options);
    }
}

