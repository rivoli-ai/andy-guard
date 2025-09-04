using Andy.Guard.Api.Infrastructure;
using Andy.Guard.Api.Models;
using Andy.Guard.AspNetCore.Options;
using Andy.Guard.Scanning.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Andy.Guard.Api.Controllers;

[ApiController]
[Route("api/output-scans")]
public sealed class OutputScansController : ControllerBase
{
    private readonly IOutputScannerRegistry _registry;
    private readonly ModelOutputScanningOptions _options;

    public OutputScansController(
        IOutputScannerRegistry registry,
        IOptions<ModelOutputScanningOptions> options)
    {
        _registry = registry;
        _options = options?.Value ?? new ModelOutputScanningOptions();
    }

    [HttpPost]
    public async Task<ActionResult<ScanResponse>> Create([FromBody] ModelOutputScanRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Output))
            return BadRequest("output is required");

        var enabled = (request.Scanners is { Count: > 0 }) ? request.Scanners : _options.EnabledScanners;
        var results = await _registry.ScanAsync(request.Prompt ?? string.Empty, request.Output, enabled, _options.ScanOptions);

        var resp = ScanResponseMapper.FromResults(request.Output, results);
        return Ok(resp);
    }
}

