using Andy.Guard.Scanning;

namespace Andy.Guard.AspNetCore.Options;

/// <summary>
/// Options for configuring model output scanning behavior in controllers/middleware.
/// </summary>
public class ModelOutputScanningOptions
{
    /// <summary>
    /// Optional list of output scanner names (case-insensitive) to run.
    /// When null or empty, all registered output scanners are executed.
    /// </summary>
    public IEnumerable<string>? EnabledScanners { get; set; }

    /// <summary>
    /// Optional per-request scan options (e.g., thresholds). If null, scanners use their defaults.
    /// </summary>
    public ScanOptions? ScanOptions { get; set; }
}
