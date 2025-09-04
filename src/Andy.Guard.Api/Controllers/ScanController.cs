using Andy.Guard.Api.Models;
using Andy.Guard.Scanning.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace Andy.Guard.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ScanController : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ScanResponse>> Post(
        [FromBody] ScanRequest request,
        [FromServices] IScannerRegistry registry)
    {
        var text = request.EffectiveText;
        if (string.IsNullOrWhiteSpace(text))
            return BadRequest("text is required");

        // Run requested scanners or all registered
        var results = await registry.ScanAsync(request.Target, text, request.Scanners);

        // Map results to findings
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

            anyDetected |= scan.IsInjectionDetected;
            maxScore = Math.Max(maxScore, scan.ConfidenceScore);
            totalMs += (long)scan.ProcessingTime.TotalMilliseconds;

            if (scan.Metadata is not null)
                mergedMeta = (mergedMeta ?? new());

            // Simple severity mapping without requiring RiskLevel type
            var sev = scan.IsInjectionDetected
                ? (scan.ConfidenceScore >= 0.85f ? Severity.High : scan.ConfidenceScore >= 0.6f ? Severity.Medium : Severity.Low)
                : Severity.Info;
            if ((int)sev > (int)highestSeverity)
                highestSeverity = sev;

            var finding = new Finding
            {
                Scanner = name,
                Code = scan.IsInjectionDetected ? "DETECTED" : "CLEAR",
                Message = scan.IsInjectionDetected ? "Indicators detected." : "No indicators detected.",
                Severity = sev,
                Confidence = scan.ConfidenceScore,
                Metadata = scan.Metadata
            };
            findings.Add(finding);
        }

        var decision = anyDetected
            ? (highestSeverity >= Severity.Medium ? Decision.Block : Decision.Review)
            : Decision.Allow;

        var resp = new ScanResponse
        {
            Decision = decision,
            Score = maxScore,
            HighestSeverity = highestSeverity,
            Findings = findings,
            Metadata = mergedMeta,
            OriginalLength = text.Length,
            ProcessingMs = totalMs
        };

        return Ok(resp);
    }
}
