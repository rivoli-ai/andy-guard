namespace Andy.Guard.Scanning.Abstractions;

/// <summary>
/// Generic text scanner abstraction capable of scanning prompts.
/// Implementations can wrap specialized scanners (e.g., prompt injection, PII).
/// </summary>
public interface IInputScanner
{
    /// <summary>
    /// Canonical scanner name
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Runs the scan for the prompt.
    /// </summary>
    Task<ScanResult> ScanAsync(string prompt, ScanOptions? options = null);
}
