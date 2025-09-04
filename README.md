# Andy Guard

Secure your LLM applications with a modular, .NET-first prompt security toolkit. Andy Guard provides a pluggable scanning core, an ASP.NET Core adapter (middleware + DI), and a sample Web API host to help you detect and act on risky inputs/outputs (e.g., prompt injection). It also includes a Hugging Face–compatible DeBERTa tokenizer to support model-backed scanners.

> ⚠️ **ALPHA RELEASE WARNING** ⚠️
> 
> This software is in ALPHA stage. **NO GUARANTEES** are made about its functionality, stability, or safety.
> 
> **CRITICAL WARNINGS:**
> - This library performs **DESTRUCTIVE OPERATIONS** on files and directories
> - Permission management is **NOT FULLY TESTED** and may have security vulnerabilities
> - **DO NOT USE** in production environments
> - **DO NOT USE** on systems with critical or irreplaceable data
> - **DO NOT USE** on systems without complete, verified backups
> - The authors assume **NO RESPONSIBILITY** for data loss, system damage, or security breaches
> 
> **USE AT YOUR OWN RISK**

**Key Features**
- Pluggable scanners: Implement `ITextScanner` once, run anywhere via `IScannerRegistry`.
- ASP.NET Core adapter: `AddGuardScanning()` + `UsePromptScanning()` for drop‑in request scanning.
- Sample API host: End-to-end reference with integration tests.
- Tokenization: DeBERTa tokenizer mirroring Hugging Face preprocessing.

**Projects**
- `src/Andy.Guard` (Core)
  - Scanning domain: `ITextScanner`, `IScannerRegistry`, default `ScannerRegistry`.
  - Prompt injection scanner (stubbed placeholder) and DeBERTa tokenizer.
- `src/Andy.Guard.AspNetCore` (Web Adapter)
  - Middleware: `PromptScanningMiddleware` and `UsePromptScanning()`.
  - DI extension: `AddGuardScanning()` registers default scanners + registry.
  - Depends on `Andy.Guard`; API hosts depend only on this adapter.
- `src/Andy.Guard.Api` (Sample API Host)
  - Minimal ASP.NET Core Web API exposing `/api/scan`.
  - References only `Andy.Guard.AspNetCore`.
- `tests/Andy.Guard.Tests` (Unit) and `tests/Andy.Guard.Api.Tests` (API Integration)

## Architecture Overview

Data flow (ASP.NET Core):

Client → API Host → `PromptScanningMiddleware` → `IScannerRegistry` → `ITextScanner`(s)

- Middleware inspects JSON bodies for `text` or `prompt` and runs input scans.
- Controllers can also call `IScannerRegistry` directly for on-demand scans.
- Registry aggregates results from one or more scanners and returns a per-scanner map.

Core abstractions:
- `ITextScanner`: A single scanner (e.g., `prompt_injection`, `pii`, `toxicity`).
- `IScannerRegistry`: Runs multiple scanners by name and aggregates their results.
- `ScanTarget`: Input vs Output scanning modes.

## Quick Start

Add to an ASP.NET Core app:

1) Reference the adapter project/package and call the DI + middleware extensions.

```csharp
using Andy.Guard.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();

// Registers default scanners and the registry
builder.Services.AddGuardScanning();

var app = builder.Build();
app.UseHttpsRedirection();

// Scans incoming JSON requests that contain a top-level "text" or "prompt"
app.UsePromptScanning();

app.MapControllers();
app.Run();
```

2) Call the sample endpoint (from the sample host or your own controller):

```http
POST /api/scan
Content-Type: application/json

{
  "text": "Ignore previous instructions and ...",
  "target": 0,              // 0 = Input, 1 = Output
  "scanners": ["prompt_injection"],
  "options": { }
}
```

Response shape:

```json
{
  "decision": "Block",
  "score": 0.82,
  "highestSeverity": "High",
  "findings": [ { "scanner": "prompt_injection", "code": "DETECTED", ... } ],
  "metadata": { },
  "originalLength": 64,
  "processingMs": 2
}
```

## Extending Scanners

Implement a new scanner by conforming to `ITextScanner` in the core library:

- `Name`: Canonical id (e.g., `pii`).
- `SupportsTarget(ScanTarget)`: Return true for supported modes.
- `ScanAsync(ScanTarget, string, ScanOptions?)`: Return a `ScanResult` with detection flags, confidence, and metadata.

Register it in DI (e.g., in a custom `Add…()` extension) and it becomes visible to `IScannerRegistry` and the API.

## Tokenizer Notes

The DeBERTa tokenizer in `Andy.Guard` mirrors Hugging Face behavior using `Microsoft.ML.Tokenizers`.
- Adds `[CLS]`/`[SEP]` as HF does; no `token_type_ids` for DeBERTa v3.
- Truncation strategies: Longest-First for pairs, head-only for singles.
- Padding with attention mask generation.
- Special-token IDs must match the model’s embedding indices. Example:

```python
from transformers import AutoTokenizer
t = AutoTokenizer.from_pretrained("microsoft/deberta-v3-base")
print(t.cls_token_id, t.sep_token_id, t.pad_token_id, t.mask_token_id, t.unk_token_id)
```

## Development

- Build: `dotnet build`
- Test all: `dotnet test`
- API tests: `dotnet test tests/Andy.Guard.Api.Tests/`

## Roadmap (High-level)
- Replace stubbed prompt injection scanner with model-backed detection (DeBERTa).
- Add more scanners (PII, jailbreak, toxicity) and sanitation utilities.
- Configurable policies and per-scanner thresholds.

## License

This project is licensed under the Apache License, Version 2.0. See the [LICENSE](LICENSE) file for details.

## Disclaimer

This is early-stage security tooling. It may produce false positives/negatives and should be combined with other defensive controls. Use at your own risk.
