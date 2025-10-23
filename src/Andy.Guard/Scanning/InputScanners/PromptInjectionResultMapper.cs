using System;
using System.Collections.Generic;
using Andy.Guard.Scanning;

namespace Andy.Guard.InputScanners;

public sealed class PromptInjectionResultMapper : IPromptInjectionResultMapper
{
    public ScanResult Map(RemoteResponse remote, ScanOptions? options, TimeSpan processingTime)
    {
        var threshold = options?.Threshold ?? remote.Threshold ?? 0.5f;
        var probability = remote.Probability ?? 0f;
        var detected = remote.IsThreat ?? probability >= threshold;
        var risk = ResolveRisk(remote.RiskLevel, detected, probability);
        Dictionary<string, object>? metadata = null;

        if (options?.IncludeMetadata != false && remote.Metadata is { Count: > 0 })
        {
            metadata = new Dictionary<string, object>(remote.Metadata.Count, StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in remote.Metadata)
            {
                if (kvp.Value is not null)
                    metadata[kvp.Key] = kvp.Value;
            }

            if (metadata.Count == 0)
                metadata = null;
        }

        return new ScanResult
        {
            IsThreatDetected = detected,
            ConfidenceScore = probability,
            RiskLevel = risk,
            Metadata = metadata,
            ProcessingTime = processingTime
        };
    }

    private static RiskLevel ResolveRisk(string? risk, bool detected, float probability)
    {
        if (!string.IsNullOrWhiteSpace(risk) && Enum.TryParse<RiskLevel>(risk, true, out var parsed))
            return parsed;

        if (!detected)
            return RiskLevel.Low;

        return probability >= 0.85f ? RiskLevel.High : RiskLevel.Medium;
    }
}
