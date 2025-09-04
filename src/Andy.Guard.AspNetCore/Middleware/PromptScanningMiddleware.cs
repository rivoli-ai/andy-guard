using System.Text.Json;
using Andy.Guard.AspNetCore.Options;
using Andy.Guard.Scanning;
using Andy.Guard.Scanning.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Andy.Guard.AspNetCore.Middleware;

/// <summary>
/// Middleware that scans incoming JSON requests using registered input scanners.
/// Looks for a top-level "text" or "prompt" string field and invokes the registry.
/// Adds aggregate scan details to <see cref="HttpContext.Items"/> and response headers.
/// </summary>
public sealed class PromptScanningMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IInputScannerRegistry _registry;
    private readonly PromptScanningOptions _options;

    public PromptScanningMiddleware(RequestDelegate next, IInputScannerRegistry registry, PromptScanningOptions options)
    {
        _next = next;
        _registry = registry;
        _options = options ?? new PromptScanningOptions();
    }

    // Support ASP.NET Core Options pattern via IOptions
    public PromptScanningMiddleware(RequestDelegate next, IInputScannerRegistry registry, IOptions<PromptScanningOptions> options)
        : this(next, registry, options?.Value ?? new PromptScanningOptions())
    { }

    public async Task InvokeAsync(HttpContext context)
    {
        IReadOnlyDictionary<string, ScanResult>? scans = null;

        if (IsJsonWithBody(context.Request))
        {
            context.Request.EnableBuffering();
            using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;

            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    string? text = TryGetText(doc.RootElement);

                    if (!string.IsNullOrEmpty(text))
                        scans = await _registry.ScanAsync(text!, _options.EnabledScanners, _options.ScanOptions);
                }
            }
            catch (JsonException)
            {
                // Ignore malformed JSON; let the pipeline handle model binding errors later.
            }
        }

        if (scans is not null)
        {
            // Expose results for downstream components
            context.Items[PromptScanningItems.InputScanResultsKey] = scans;

            // Aggregate for simple visibility via headers
            var anyDetected = scans.Values.Any(r => r.IsThreatDetected);
            var highestRisk = scans.Values.Select(r => r.RiskLevel).DefaultIfEmpty(RiskLevel.Low).Max();
            var maxConfidence = scans.Values.Select(r => r.ConfidenceScore).DefaultIfEmpty(0f).Max();

            context.Response.Headers["X-Guard-Scan-Detected"] = anyDetected.ToString();
            context.Response.Headers["X-Guard-Scan-Risk"] = highestRisk.ToString();
            context.Response.Headers["X-Guard-Scan-Confidence"] = maxConfidence.ToString("0.###");
            context.Response.Headers["X-Guard-Scan-Scanners"] = string.Join(',', scans.Keys);

            // Optionally short-circuit on detected threat
            if (anyDetected && _options.BlockOnThreat)
            {
                if (!context.Response.HasStarted)
                {
                    context.Response.StatusCode = _options.ThreatResponseStatusCode;

                    if (_options.IncludeResponseDetails)
                    {
                        context.Response.ContentType = "application/json";

                        var details = new
                        {
                            message = "Prompt blocked by guard policy.",
                            detected = true,
                            highestRisk = highestRisk.ToString(),
                            maxConfidence,
                            scanners = scans.ToDictionary(
                                kvp => kvp.Key,
                                kvp => new
                                {
                                    isThreatDetected = kvp.Value.IsThreatDetected,
                                    confidence = kvp.Value.ConfidenceScore,
                                    risk = kvp.Value.RiskLevel.ToString(),
                                    processingTimeMs = kvp.Value.ProcessingTime.TotalMilliseconds
                                })
                        };

                        var json = JsonSerializer.Serialize(details);
                        await context.Response.WriteAsync(json);
                    }
                }

                return; // Short-circuit: do not call next middleware
            }
        }

        await _next(context);
    }

    private static bool IsJsonWithBody(HttpRequest request)
    {
        if (request.ContentLength is null or <= 0)
            return false;
        var contentType = request.ContentType ?? string.Empty;
        return contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryGetText(JsonElement root)
    {
        if (root.TryGetProperty("prompt", out var promptProp) && promptProp.ValueKind == JsonValueKind.String)
            return promptProp.GetString();
        if (root.TryGetProperty("text", out var textProp) && textProp.ValueKind == JsonValueKind.String)
            return textProp.GetString();
        return null;
    }
}

public static class PromptScanningMiddlewareExtensions
{
    /// <summary>
    /// Scans using all registered input scanners.
    /// </summary>
    public static IApplicationBuilder UsePromptScanning(this IApplicationBuilder app)
        => app.UseMiddleware<PromptScanningMiddleware>();

    /// <summary>
    /// Scans using the specified input scanner names (case-insensitive). Unknown names are ignored.
    /// </summary>
    public static IApplicationBuilder UsePromptScanning(this IApplicationBuilder app, params string[] scannerNames)
        => app.UseMiddleware<PromptScanningMiddleware>(new PromptScanningOptions
        {
            EnabledScanners = (scannerNames ?? Array.Empty<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).ToArray()
        });

    /// <summary>
    /// Scans using custom options (e.g., enabled scanners, thresholds).
    /// </summary>
    public static IApplicationBuilder UsePromptScanning(this IApplicationBuilder app, PromptScanningOptions options)
        => app.UseMiddleware<PromptScanningMiddleware>(options ?? new PromptScanningOptions());
}

/// <summary>
/// Constants and helpers for exposing scan results.
/// </summary>
public static class PromptScanningItems
{
    /// <summary>
    /// Key used to store input scan results in <see cref="HttpContext.Items"/>.
    /// Value type: <see cref="IReadOnlyDictionary{TKey, TValue}"/> of scanner name to <see cref="ScanResult"/>.
    /// </summary>
    public const string InputScanResultsKey = "Andy.Guard.InputScanResults";
}
