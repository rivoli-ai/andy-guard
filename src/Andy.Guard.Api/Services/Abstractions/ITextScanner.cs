using Andy.Guard.InputScanners;

namespace Andy.Guard.Api.Services.Abstractions;

/// <summary>
/// Generic text scanner abstraction capable of scanning input or output text.
/// Implementations can wrap specialized scanners (e.g., prompt injection, PII).
/// </summary>
public interface ITextScanner
{
    /// <summary>
    /// Canonical scanner name (e.g., "prompt_injection", "pii").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Whether the scanner supports the given target (input/output).
    /// </summary>
    bool SupportsTarget(Models.ScanTarget target) => true;

    /// <summary>
    /// Runs the scan for the given target and text.
    /// </summary>
    Task<ScanResult> ScanAsync(Models.ScanTarget target, string text, ScanOptions? options = null);
}

