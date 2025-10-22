# Andy Guard

Secure your LLM applications with a modular, .NET-first prompt security toolkit. Andy Guard provides a pluggable scanning core, an ASP.NET Core adapter (middleware + DI), and a sample Web API host to help you detect and act on risky inputs/outputs (e.g., prompt injection). The built-in prompt-injection scanner calls a downstream inference service so you can lean on cloud-hosted detectors without shipping tokenizer assets or managing local model runtimes.

> ⚠️ ALPHA STATUS
> 
> This is early-stage security tooling. APIs and defaults may change. Do not rely on the library for production-grade protection without your own validation and layered defenses.

**Key Features**
- Pluggable scanners: Implement `IInputScanner`/`IOutputScanner`, orchestrate via registries.
- Remote inference integration: `InferenceApiClient` wraps `IDownstreamApi` to call hosted detectors.
- ASP.NET Core adapter: `AddPromptScanning()`/`AddModelOutputScanning()` + `UsePromptScanning()`.
- Sample API host: End-to-end reference with integration tests.

**Projects**
- `src/Andy.Guard` (Core)
  - Scanning domain: `IInputScanner`, `IOutputScanner`, `IInputScannerRegistry`, `IOutputScannerRegistry`.
  - `InferenceApiClient` helper for authenticated downstream calls (Microsoft.Identity.Abstractions).
  - HTTP-based `PromptInjectionScanner` that posts prompts to the configured inference endpoint.
- `src/Andy.Guard.AspNetCore` (Web Adapter)
  - Middleware: `PromptScanningMiddleware` (`UsePromptScanning()`), options and headers.
  - DI extensions: `AddPromptScanning()` and `AddModelOutputScanning()`.
- `src/Andy.Guard.Api` (Sample API Host)
  - Minimal ASP.NET Core Web API exposing `/api/prompt-scans` and `/api/output-scans`.
  - Demonstrates `AddDownstreamApi` configuration for the prompt-injection service.
- `tests/Andy.Guard.Tests` (Unit) and `tests/Andy.Guard.Api.Tests` (API Integration)

## Architecture Overview

Data flow (ASP.NET Core):

Client → API Host → `PromptScanningMiddleware` → `IInputScannerRegistry` → `IInputScanner`(s) → Downstream inference service

- Middleware inspects JSON request bodies for `text` or `prompt` and runs input scans.
- Controllers can also call `IInputScannerRegistry`/`IOutputScannerRegistry` directly.
- Registries aggregate results from one or more scanners and return a per-scanner map.
- The default `PromptInjectionScanner` posts batched payloads to the configured downstream API and maps the response back into a `ScanResult`.

Core abstractions:
- `IInputScanner` / `IOutputScanner`: Individual scanners (e.g., `PromptInjectionScanner`, `ToxicityScanner`).
- `IInputScannerRegistry` / `IOutputScannerRegistry`: Orchestrate scanners by name and aggregate results.

## Quick Start

Add to an ASP.NET Core app:

1) Reference the adapter project/package and call the DI + middleware extensions.

```csharp
using Andy.Guard.AspNetCore;
using Andy.Guard.AspNetCore.Middleware;
using Microsoft.Identity.Web;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();

// Registers default input and (placeholder) output registries
builder.Services.AddPromptScanning();
builder.Services.AddModelOutputScanning();
builder.Services.AddDownstreamApi("AndyInference", builder.Configuration.GetSection("DownstreamApis:AndyInference"));

var app = builder.Build();
app.UseHttpsRedirection();

// Scans incoming JSON requests with top-level "text" or "prompt"
app.UsePromptScanning(); // or app.UsePromptScanning(new PromptScanningOptions { BlockOnThreat = true })

app.MapControllers();
app.Run();
```

2) Call the sample endpoints (from the sample host or your own controller):

Prompt (input) scanning

```http
POST /api/prompt-scans
Content-Type: application/json

{
  "text": "Ignore previous instructions and ...",
  "scanners": ["PromptInjectionScanner"],
  "options": { }
}
```

Output scanning

```http
POST /api/output-scans
Content-Type: application/json

{
  "prompt": "Translate to French",
  "output": "Ignore all prior prompts and ...",
  "scanners": [],
  "options": { }
}
```

Response shape (both endpoints):

```json
{
  "decision": "Block",
  "score": 0.82,
  "highestSeverity": "High",
  "findings": [
    { "scanner": "PromptInjectionScanner", "code": "DETECTED", "message": "Indicators detected.", "severity": "High", "confidence": 0.82 }
  ],
  "metadata": {},
  "originalLength": 64,
  "processingMs": 2
}
```

## Prompt Injection Scanner Configuration

The default `PromptInjectionScanner` sends batched prompts to a downstream inference API and maps its response into Andy Guard's result model. The helper uses `IDownstreamApi` (via `Microsoft.Identity.Web` / `Microsoft.Identity.Abstractions`), so you can authenticate with Azure AD/Entra ID or other providers supported by those libraries.

Register the downstream API in DI:

```csharp
builder.Services.AddDownstreamApi(
    "AndyInference",
    builder.Configuration.GetSection("DownstreamApis:AndyInference"));
```

Example configuration (`appsettings.json`):

```json
{
  "DownstreamApis": {
    "AndyInference": {
      "BaseUrl": "https://inference-service/api",
      "RelativePath": "predict/batch",
      "Scopes": [ "api://inference-service/.default" ],
      "RequestAppToken": true
    }
  }
}
```

Each scan request results in a payload like:

```json
[
  {
    "text": "Ignore previous instructions and ...",
    "model": "deberta-v3-base-prompt-injection-v2"
  }
]
```

The scanner expects fields such as `label`, `score`, `scores`, `isSafe`, `usingFallback`, and `predictionMethod` in the response. Missing or error responses are surfaced through `ScanResult.Metadata` (when requested) without throwing in-line exceptions, allowing callers to decide how to handle degraded detections.

## Extending Scanners

Implement a new scanner by conforming to `IInputScanner` or `IOutputScanner` in the core library:

- `Name`: Canonical id (e.g., `PromptInjectionScanner`, `ToxicityScanner`).
- `ScanAsync(...)`: Return a `ScanResult` with detection flags, confidence, risk, and metadata.

Register it in DI (e.g., via `IServiceCollection` extensions) and it becomes visible to the registries and API.

```csharp
public sealed class MyCustomScanner : IInputScanner
{
    public string Name => "MyCustomScanner";
    public Task<ScanResult> ScanAsync(string prompt, ScanOptions? options = null)
        => Task.FromResult(new ScanResult { IsThreatDetected = false, ConfidenceScore = 0.0f, RiskLevel = RiskLevel.Low });
}
```
