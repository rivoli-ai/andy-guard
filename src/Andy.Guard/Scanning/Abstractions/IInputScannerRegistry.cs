namespace Andy.Guard.Scanning.Abstractions;

/// <summary>
/// Aggregates available scanners and orchestrates running one or more by name.
/// </summary>
public interface IInputScannerRegistry
{
    /// <summary>
    /// Returns the registered scanner names.
    /// </summary>
    IReadOnlyCollection<string> RegisteredScanners { get; }

    /// <summary>
    /// Scans prompt with the specified input scanners; when null/empty, runs all available scanners.
    /// </summary>
    Task<IReadOnlyDictionary<string, ScanResult>> ScanAsync(
        string prompt,
        IEnumerable<string>? scanners = null,
        ScanOptions? options = null);
}
