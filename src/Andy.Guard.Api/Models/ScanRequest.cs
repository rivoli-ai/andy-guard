using System.Text.Json.Serialization;

namespace Andy.Guard.Api.Models;

/// <summary>
/// Generic scan request payload that can support multiple scanner types and input/output targets.
/// </summary>
public sealed class ScanRequest
{
    /// <summary>
    /// The text to scan.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Backward-compatible alias for Text (accepts { "prompt": "..." }).
    /// </summary>
    public string? Prompt { get; set; }

    /// <summary>
    /// Whether this is input (pre-LLM) or output (post-LLM) scanning.
    /// </summary>
    public ScanTarget Target { get; set; } = ScanTarget.Input;

    /// <summary>
    /// Optional list of scanners to run (e.g., ["prompt_injection"]). Empty means defaults.
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

    /// <summary>
    /// Convenience accessor for the effective text (Text or Prompt fallback).
    /// </summary>
    [JsonIgnore]
    public string EffectiveText => string.IsNullOrWhiteSpace(Text) ? (Prompt ?? string.Empty) : Text;
}

