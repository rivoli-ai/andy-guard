namespace Andy.Guard.Api.Models;

/// <summary>
/// Represents a single scanner finding (generic across scanner types).
/// </summary>
public sealed class Finding
{
    public string Scanner { get; set; } = "prompt_injection"; // e.g., prompt_injection, pii, jailbreak
    public string Code { get; set; } = "generic";             // implementation-specific code/id
    public string Message { get; set; } = string.Empty;        // brief human-readable summary
    public Severity Severity { get; set; } = Severity.Low;
    public float Confidence { get; set; } = 0.0f;
    public int? Start { get; set; }                            // optional character offset
    public int? Length { get; set; }                           // optional length
    public Dictionary<string, object>? Metadata { get; set; }  // scanner-specific extras
}

