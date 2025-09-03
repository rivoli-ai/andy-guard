using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Andy.Guard.InputScanners;
using Andy.Guard.InputScanners.Abstractions;
using Microsoft.AspNetCore.Http;

namespace Andy.Guard.Api.Middleware;

/// <summary>
/// Middleware that scans incoming JSON requests for prompt injections.
/// Looks for a top-level "prompt" string field and invokes the scanner.
/// Adds basic scan details to <see cref="HttpContext.Items"/> and response headers.
/// </summary>
public sealed class PromptScanningMiddleware
{
    private readonly RequestDelegate _next;

    public PromptScanningMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IPromptInjectionScanner scanner)
    {
        ScanResult? scan = null;

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
                    string? text = null;
                    if (doc.RootElement.TryGetProperty("prompt", out var promptProp) && promptProp.ValueKind == JsonValueKind.String)
                        text = promptProp.GetString();
                    else if (doc.RootElement.TryGetProperty("text", out var textProp) && textProp.ValueKind == JsonValueKind.String)
                        text = textProp.GetString();

                    if (!string.IsNullOrEmpty(text))
                    {
                        scan = await scanner.ScanInputAsync(text);
                    }
                }
            }
            catch (JsonException)
            {
                // Ignore malformed JSON; let the pipeline handle model binding errors later.
            }
        }

        if (scan is not null)
        {
            context.Items[nameof(ScanResult)] = scan;
            context.Response.Headers["X-Guard-Scan-Detected"] = scan.IsInjectionDetected.ToString();
            context.Response.Headers["X-Guard-Scan-Risk"] = scan.RiskLevel.ToString();
            context.Response.Headers["X-Guard-Scan-Confidence"] = scan.ConfidenceScore.ToString("0.###");
        }

        await _next(context);
    }

    private static bool IsJsonWithBody(HttpRequest request)
    {
        if (request.ContentLength is null or <= 0) return false;
        var contentType = request.ContentType ?? string.Empty;
        return contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase);
    }
}

public static class PromptScanningMiddlewareExtensions
{
    public static IApplicationBuilder UsePromptScanning(this IApplicationBuilder app)
        => app.UseMiddleware<PromptScanningMiddleware>();
}
