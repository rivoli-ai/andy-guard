using Andy.Guard.Scanning;
using Microsoft.AspNetCore.Http;

namespace Andy.Guard.AspNetCore.Options;

/// <summary>
/// Options for configuring the PromptScanningMiddleware.
/// </summary>
public sealed class PromptScanningOptions
{
    /// <summary>
    /// Optional list of scanner names (case-insensitive) to run.
    /// When null or empty, all registered input scanners are executed.
    /// </summary>
    public IEnumerable<string>? EnabledScanners { get; set; }

    /// <summary>
    /// Optional per-request scan options (e.g., thresholds). If null, scanners use their defaults.
    /// </summary>
    public ScanOptions? ScanOptions { get; set; }

    /// <summary>
    /// When true, short-circuits the pipeline and returns a 4xx response if any scanner detects a threat.
    /// </summary>
    public bool BlockOnThreat { get; set; } = true;

    /// <summary>
    /// HTTP status code to return when blocking. Defaults to 400.
    /// </summary>
    public int ThreatResponseStatusCode { get; set; } = StatusCodes.Status400BadRequest;

    /// <summary>
    /// When true, writes a small JSON payload with detection details in the response body upon blocking.
    /// </summary>
    public bool IncludeResponseDetails { get; set; } = true;
}
