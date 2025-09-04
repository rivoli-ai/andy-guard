namespace Andy.Guard.Scanning;

public class ScanResult
{
    public bool IsThreatDetected { get; set; }
    public float ConfidenceScore { get; set; }
    public RiskLevel RiskLevel { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
    public TimeSpan ProcessingTime { get; set; }
}
