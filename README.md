# Andy Guard

Secure your LLM applications with a modular, .NET‑first prompt security toolkit. Andy Guard provides a pluggable scanning core, an ASP.NET Core adapter (middleware + DI), and a sample Web API host to help you detect and act on risky inputs/outputs (e.g., prompt injection). It also ships a Hugging Face–compatible DeBERTa tokenizer and optional ONNX runtime support for model‑backed scanners.

> ⚠️ ALPHA STATUS
> 
> This is early‑stage security tooling. APIs and defaults may change. Do not rely on the library for production‑grade protection without your own validation and layered defenses.

**Key Features**
- Pluggable scanners: Implement `IInputScanner`/`IOutputScanner`, orchestrate via registries.
- ASP.NET Core adapter: `AddPromptScanning()`/`AddModelOutputScanning()` + `UsePromptScanning()`.
- Sample API host: End‑to‑end reference with integration tests.
- Tokenization: DeBERTa tokenizer with HF parity tests; optional ONNX inference.

**Projects**
- `src/Andy.Guard` (Core)
  - Scanning domain: `IInputScanner`, `IOutputScanner`, `IInputScannerRegistry`, `IOutputScannerRegistry`.
  - Prompt injection scanner with heuristics + optional DeBERTa ONNX model.
  - DeBERTa tokenizer using `Microsoft.ML.Tokenizers`.
- `src/Andy.Guard.AspNetCore` (Web Adapter)
  - Middleware: `PromptScanningMiddleware` (`UsePromptScanning()`), options and headers.
  - DI extensions: `AddPromptScanning()` and `AddModelOutputScanning()`.
- `src/Andy.Guard.Api` (Sample API Host)
  - Minimal ASP.NET Core Web API exposing `/api/prompt-scans` and `/api/output-scans`.
- `tests/Andy.Guard.Tests` (Unit) and `tests/Andy.Guard.Api.Tests` (API Integration)

## Architecture Overview

Data flow (ASP.NET Core):

Client → API Host → `PromptScanningMiddleware` → `IInputScannerRegistry` → `IInputScanner`(s)

- Middleware inspects JSON request bodies for `text` or `prompt` and runs input scans.
- Controllers can also call `IInputScannerRegistry`/`IOutputScannerRegistry` directly.
- Registries aggregate results from one or more scanners and return a per‑scanner map.

Core abstractions:
- `IInputScanner` / `IOutputScanner`: Individual scanners (e.g., `prompt_injection`, `toxicity`).
- `IInputScannerRegistry` / `IOutputScannerRegistry`: Orchestrate scanners by name and aggregate results.

## Quick Start

Add to an ASP.NET Core app:

1) Reference the adapter project/package and call the DI + middleware extensions.

```csharp
using Andy.Guard.AspNetCore;
using Andy.Guard.AspNetCore.Middleware;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();

// Registers default input and (placeholder) output registries
builder.Services.AddPromptScanning();
builder.Services.AddModelOutputScanning();

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
  "scanners": ["prompt_injection"],
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
    { "scanner": "prompt_injection", "code": "DETECTED", "message": "Indicators detected.", "severity": "High", "confidence": 0.82 }
  ],
  "metadata": {},
  "originalLength": 64,
  "processingMs": 2
}
```

## Extending Scanners

Implement a new scanner by conforming to `IInputScanner` or `IOutputScanner` in the core library:

- `Name`: Canonical id (e.g., `prompt_injection`, `toxicity`).
- `ScanAsync(...)`: Return a `ScanResult` with detection flags, confidence, risk, and metadata.

Register it in DI (e.g., via `IServiceCollection` extensions) and it becomes visible to the registries and API.

```csharp
public sealed class MyCustomScanner : IInputScanner
{
    public string Name => "my_custom";
    public Task<ScanResult> ScanAsync(string prompt, ScanOptions? options = null)
        => Task.FromResult(new ScanResult { IsThreatDetected = false, ConfidenceScore = 0.0f, RiskLevel = RiskLevel.Low });
}
```

## Tokenizer & Model Notes

The DeBERTa tokenizer in `Andy.Guard` mirrors Hugging Face behavior using `Microsoft.ML.Tokenizers`.
- Adds `[CLS]`/`[SEP]` as HF does; no `token_type_ids` for DeBERTa v3.
- Truncation strategies: Longest‑First for pairs, head‑only for singles.
- Padding with attention mask generation.
- Special‑token IDs must match the model’s embedding indices. Retrieve from HF once:

```python
from transformers import AutoTokenizer
t = AutoTokenizer.from_pretrained("microsoft/deberta-v3-base")
print(t.cls_token_id, t.sep_token_id, t.pad_token_id, t.mask_token_id, t.unk_token_id)
```

Included assets (for development and tests):
- `src/Andy.Guard/Tokenizers/Deberta/onnx/spm.model` (copied to output as `./onnx/spm.model`)
- `src/Andy.Guard/Tokenizers/Deberta/onnx/tokenizer.json`, `config.json`

To enable tokenizer/inference in the prompt‑injection scanner, set environment variables:

- `ANDY_GUARD_DEBERTA_SPM_PATH`: Path to SentencePiece `spm.model` (e.g., `./onnx/spm.model`).
- `ANDY_GUARD_DEBERTA_MAX_LEN`: Max sequence length (default `512`).
- `ANDY_GUARD_DEBERTA_CLS_ID`, `ANDY_GUARD_DEBERTA_SEP_ID`, `ANDY_GUARD_DEBERTA_PAD_ID`, `ANDY_GUARD_DEBERTA_MASK_ID`, `ANDY_GUARD_DEBERTA_UNK_ID`.
- `ANDY_GUARD_PI_THRESHOLD`: Probability threshold for detection (default `0.5`).
- `ANDY_GUARD_PI_ONNX_PATH`: Path to DeBERTa ONNX model (optional). If not set, the scanner either uses heuristics or will try to download from Hugging Face (see below).

Model download from Hugging Face:
- By default, if `ANDY_GUARD_PI_ONNX_PATH` is not set, the scanner will attempt to fetch `onnx/model.onnx` from `protectai/deberta-v3-base-prompt-injection-v2` on Hugging Face and store it at `./onnx/model.onnx` at runtime.
- You can customize this via:
  - `ANDY_GUARD_PI_ONNX_HF_REPO` (default `protectai/deberta-v3-base-prompt-injection-v2`)
  - `ANDY_GUARD_PI_ONNX_HF_REVISION` (default `main`)
  - `ANDY_GUARD_PI_ONNX_FILENAME` (default `onnx/model.onnx`)
  - `ANDY_GUARD_PI_ONNX_LOCAL_PATH` (default `./onnx/model.onnx` under the app base directory)

Note: Only `spm.model` is shipped in the repo to avoid large binaries. The ONNX model is downloaded on‑demand or can be provided via a local path.

## Development

- Build: `dotnet build`
- Test all: `dotnet test`
- API tests: `dotnet test tests/Andy.Guard.Api.Tests/`

## Roadmap (High‑level)
- Strengthen prompt‑injection with additional signals and training data.
- Add more scanners (PII, jailbreak, toxicity) and sanitation utilities.
- Configurable policies and per‑scanner thresholds.

## License

This project is licensed under the Apache License, Version 2.0. See the [LICENSE](LICENSE) file for details.

## Disclaimer

This is early‑stage security tooling. It may produce false positives/negatives and should be combined with other defensive controls. Use at your own risk.
