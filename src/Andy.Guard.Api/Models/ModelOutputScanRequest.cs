namespace Andy.Guard.Api.Models;

/// <summary>
/// Scan request payload for model output scanning. Includes original prompt (optional) and model output (required).
/// </summary>
public sealed class ModelOutputScanRequest
{
    /// <summary>
    /// The original prompt that produced the output (optional but recommended for context-aware scanners).
    /// </summary>
    public string? Prompt { get; set; }

    /// <summary>
    /// The model output to scan.
    /// </summary>
    public string Output { get; set; } = string.Empty;

    /// <summary>
    /// Optional list of scanners to run (e.g., ["toxicity"]). Empty means defaults.
    /// </summary>
    public List<string>? Scanners { get; set; }

    /// <summary>
    /// Optional per-request parameters for scanners.
    /// </summary>
    public Dictionary<string, object>? Options { get; set; }

    /// <summary>
    /// Optional context fields (e.g., user role, application hints).
    /// </summary>
    public Dictionary<string, string>? Context { get; set; }
}

