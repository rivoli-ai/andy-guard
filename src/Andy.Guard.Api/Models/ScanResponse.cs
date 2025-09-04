namespace Andy.Guard.Api.Models;

/// <summary>
/// Generic scan response aggregating findings and an overall decision.
/// </summary>
public sealed class ScanResponse
{
    public Decision Decision { get; set; } = Decision.Allow;
    public float Score { get; set; } = 0.0f; // aggregate confidence/risk score [0,1]
    public Severity HighestSeverity { get; set; } = Severity.Info;
    public List<Finding> Findings { get; set; } = new();
    public Dictionary<string, object>? Metadata { get; set; }

    // Optional fields that other scanners could use
    public string? SanitizedText { get; set; }

    // Diagnostics
    public int OriginalLength { get; set; }
    public long ProcessingMs { get; set; }
}
