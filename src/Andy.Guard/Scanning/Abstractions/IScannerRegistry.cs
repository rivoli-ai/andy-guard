using Andy.Guard.InputScanners;
using Andy.Guard.Scanning;

namespace Andy.Guard.Scanning.Abstractions;

/// <summary>
/// Aggregates available scanners and orchestrates running one or more by name.
/// </summary>
public interface IScannerRegistry
{
    /// <summary>
    /// Returns the registered scanner names.
    /// </summary>
    IReadOnlyCollection<string> RegisteredScanners { get; }

    /// <summary>
    /// Scans text with the specified scanners; when null/empty, runs all available scanners.
    /// </summary>
    Task<IReadOnlyDictionary<string, ScanResult>> ScanAsync(
        ScanTarget target,
        string text,
        IEnumerable<string>? scanners = null,
        ScanOptions? options = null);
}
