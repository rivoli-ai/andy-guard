using Andy.Guard.Api.Models;
using Andy.Guard.Scanning;

namespace Andy.Guard.Api.Infrastructure;

internal static class ScanResponseMapper
{
    public static ScanResponse FromResults(string scannedText, IReadOnlyDictionary<string, ScanResult> results)
    {
        var findings = new List<Finding>();
        bool anyDetected = false;
        float maxScore = 0f;
        long totalMs = 0;
        Dictionary<string, object>? mergedMeta = null;
        Severity highestSeverity = Severity.Info;

        foreach (var kv in results)
        {
            var name = kv.Key;
            var scan = kv.Value;

            anyDetected |= scan.IsThreatDetected;
            maxScore = Math.Max(maxScore, scan.ConfidenceScore);
            totalMs += (long)scan.ProcessingTime.TotalMilliseconds;

            if (scan.Metadata is not null)
            {
                mergedMeta ??= new();
                if (!mergedMeta.TryGetValue("scanners", out var scannersObj) || scannersObj is not Dictionary<string, object> scannersDict)
                {
                    scannersDict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    mergedMeta["scanners"] = scannersDict;
                }
                scannersDict[name] = scan.Metadata;
            }

            var sev = scan.IsThreatDetected
                ? (scan.ConfidenceScore >= 0.85f ? Severity.High : scan.ConfidenceScore >= 0.6f ? Severity.Medium : Severity.Low)
                : Severity.Info;
            if ((int)sev > (int)highestSeverity)
                highestSeverity = sev;

            findings.Add(new Finding
            {
                Scanner = name,
                Code = scan.IsThreatDetected ? "DETECTED" : "CLEAR",
                Message = scan.IsThreatDetected ? "Indicators detected." : "No indicators detected.",
                Severity = sev,
                Confidence = scan.ConfidenceScore,
                Metadata = scan.Metadata
            });
        }

        var decision = anyDetected
            ? (highestSeverity >= Severity.Medium ? Decision.Block : Decision.Review)
            : Decision.Allow;

        return new ScanResponse
        {
            Decision = decision,
            Score = maxScore,
            HighestSeverity = highestSeverity,
            Findings = findings,
            Metadata = mergedMeta,
            OriginalLength = scannedText?.Length ?? 0,
            ProcessingMs = totalMs
        };
    }
}
