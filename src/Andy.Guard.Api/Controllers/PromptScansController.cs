using Andy.Guard.Api.Infrastructure;
using Andy.Guard.Api.Models;
using Andy.Guard.AspNetCore.Options;
using Andy.Guard.Scanning.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Andy.Guard.Api.Controllers;

[ApiController]
[Route("api/prompt-scans")]
public sealed class PromptScansController : ControllerBase
{
    private readonly IInputScannerRegistry _registry;
    private readonly PromptScanningOptions _options;

    public PromptScansController(
        IInputScannerRegistry registry,
        IOptions<PromptScanningOptions> options)
    {
        _registry = registry;
        _options = options?.Value ?? new PromptScanningOptions();
    }

    [HttpPost]
    public async Task<ActionResult<ScanResponse>> Create([FromBody] ScanRequest request)
    {
        var text = request.EffectiveText;
        if (string.IsNullOrWhiteSpace(text))
            return BadRequest("text is required");

        var enabled = (request.Scanners is { Count: > 0 }) ? request.Scanners : _options.EnabledScanners;
        var results = await _registry.ScanAsync(text, enabled, _options.ScanOptions);

        var resp = ScanResponseMapper.FromResults(text, results);
        return Ok(resp);
    }
}

