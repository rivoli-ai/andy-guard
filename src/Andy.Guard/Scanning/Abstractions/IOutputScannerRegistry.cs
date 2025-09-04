namespace Andy.Guard.Scanning.Abstractions;

/// <summary>
/// Aggregates available output scanners and orchestrates running one or more by name.
/// </summary>
public interface IOutputScannerRegistry
{
    /// <summary>
    /// Returns the registered output scanner names.
    /// </summary>
    IReadOnlyCollection<string> RegisteredOutputScanners { get; }

    /// <summary>
    /// Scans output of model with the specified output scanners; when null/empty, runs all available scanners.
    /// </summary>
    Task<IReadOnlyDictionary<string, ScanResult>> ScanAsync(
        string prompt,
        string output,
        IEnumerable<string>? scanners = null,
        ScanOptions? options = null);
}
