using System.Collections.Generic;

namespace Andy.Guard.InputScanners;

public sealed class RemoteResponse
{
    public float? Probability { get; init; }
    public bool? IsThreat { get; init; }
    public string? RiskLevel { get; init; }
    public float? Threshold { get; init; }
    public Dictionary<string, object?>? Metadata { get; init; }
}
