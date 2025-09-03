using Andy.Guard.Api.Models;
using Andy.Guard.Api.Services.Abstractions;
using Andy.Guard.InputScanners;
using Andy.Guard.InputScanners.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace Andy.Guard.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ScanController : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ScanResponse>> Post(
        [FromBody] ScanRequest request,
        [FromServices] IScannerRegistry registry,
        [FromServices] IPromptInjectionScanner fallbackScanner)
    {
        var text = request.EffectiveText;
        if (string.IsNullOrWhiteSpace(text))
            return BadRequest("text is required");

        // Prefer result from middleware for Input scans by caching under the registry name
        Dictionary<string, ScanResult>? seed = null;
        if (request.Target == ScanTarget.Input &&
            HttpContext.Items.TryGetValue(nameof(ScanResult), out var middleware) && middleware is ScanResult cached)
        {
            seed = new Dictionary<string, ScanResult>(StringComparer.OrdinalIgnoreCase)
            {
                ["prompt_injection"] = cached
            };
        }

        // Run requested scanners or all registered; merge with seed if present
        var results = await registry.ScanAsync(request.Target, text, request.Scanners);
        if (seed is not null)
        {
            foreach (var kv in seed)
                results = new Dictionary<string, ScanResult>(results, StringComparer.OrdinalIgnoreCase) {{ kv.Key, kv.Value }};
        }

        // Fallback: if registry returned nothing (e.g., misconfig), run prompt_injection directly
        if (results.Count == 0)
        {
            var direct = request.Target == ScanTarget.Input
                ? await fallbackScanner.ScanInputAsync(text)
                : await fallbackScanner.ScanOutputAsync(text);
            results = new Dictionary<string, ScanResult>(StringComparer.OrdinalIgnoreCase) {{ "prompt_injection", direct }};
        }

        // Map results to findings
        var findings = new List<Finding>();
        bool anyDetected = false;
        float maxScore = 0f;
        var maxRisk = Andy.Guard.RiskLevel.Undefined;
        long totalMs = 0;
        Dictionary<string, object>? mergedMeta = null;

        foreach (var kv in results)
        {
            var name = kv.Key;
            var scan = kv.Value;

            anyDetected |= scan.IsInjectionDetected;
            maxScore = Math.Max(maxScore, scan.ConfidenceScore);
            if ((int)scan.RiskLevel > (int)maxRisk) maxRisk = scan.RiskLevel;
            totalMs += (long)scan.ProcessingTime.TotalMilliseconds;

            if (scan.Metadata is not null)
                mergedMeta = (mergedMeta ?? new());

            var finding = new Finding
            {
                Scanner = name,
                Code = scan.IsInjectionDetected ? "DETECTED" : "CLEAR",
                Message = scan.IsInjectionDetected ? "Indicators detected." : "No indicators detected.",
                Severity = scan.RiskLevel switch
                {
                    Andy.Guard.RiskLevel.Critical => Severity.Critical,
                    Andy.Guard.RiskLevel.High => Severity.High,
                    Andy.Guard.RiskLevel.Medium => Severity.Medium,
                    Andy.Guard.RiskLevel.Low => Severity.Low,
                    _ => Severity.Info
                },
                Confidence = scan.ConfidenceScore,
                Metadata = scan.Metadata
            };
            findings.Add(finding);
        }

        var decision = anyDetected
            ? (maxRisk >= Andy.Guard.RiskLevel.Medium ? Decision.Block : Decision.Review)
            : Decision.Allow;

        var resp = new ScanResponse
        {
            Decision = decision,
            Score = maxScore,
            Risk = maxRisk,
            Findings = findings,
            Metadata = mergedMeta,
            OriginalLength = text.Length,
            ProcessingMs = totalMs
        };

        return Ok(resp);
    }
}
