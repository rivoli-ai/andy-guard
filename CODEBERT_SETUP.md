# CodeBERT Setup Guide

This guide explains how to configure the CodeBERT scanner in Andy Guard.

## Environment Variables Required

The CodeBERT scanner reuses the existing DeBERTa environment variables for compatibility:

### 1. Model Configuration (Uses DeBERTa Variables)
```bash
# Detection threshold (0.0 to 1.0, default: 0.5)
ANDY_GUARD_PI_THRESHOLD=0.5

# Maximum sequence length (default: 512)
ANDY_GUARD_DEBERTA_MAX_LEN=512

# Path to ONNX model (optional - can use DeBERTa model path)
ANDY_GUARD_PI_ONNX_PATH=./onnx/codebert_model.onnx
```

### 2. Tokenizer Configuration (Uses DeBERTa Variables)
```bash
# Path to vocabulary file (required for tokenizer)
ANDY_GUARD_DEBERTA_VOCAB_PATH=./tokenizer/vocab.txt

# Path to merges file (required for tokenizer)
ANDY_GUARD_DEBERTA_MERGES_PATH=./tokenizer/merges.txt

# Special token IDs (same as DeBERTa/BERT)
ANDY_GUARD_DEBERTA_CLS_ID=101
ANDY_GUARD_DEBERTA_SEP_ID=102
ANDY_GUARD_DEBERTA_PAD_ID=0
ANDY_GUARD_DEBERTA_MASK_ID=103
ANDY_GUARD_DEBERTA_UNK_ID=100
```

## Setup Options

### Option 1: Heuristics Only (No Model Required)
If you don't have the CodeBERT model files, the scanner will fall back to heuristic detection:

```bash
# Set only the threshold - no model files needed
ANDY_GUARD_PI_THRESHOLD=0.5
```

The scanner will use pattern matching to detect:
- `exec()`, `eval()`, `system()` calls
- `innerHTML`, `document.write` DOM manipulation
- `Process.Start`, `Runtime.getRuntime` system access
- Suspicious keywords (password, secret, key, etc.)

### Option 2: Full CodeBERT Model (Recommended)
For best accuracy, download the CodeBERT model and tokenizer files:

1. **Download CodeBERT Model:**
   ```bash
   # Create directories
   mkdir onnx
   mkdir tokenizer
   
   # Download model (example - replace with actual CodeBERT model)
   # You'll need to convert CodeBERT to ONNX format
   ```

2. **Download Tokenizer Files:**
   ```bash
   # Download from Hugging Face or Microsoft's CodeBERT repository
   # You need vocab.txt and merges.txt files
   ```

3. **Set Environment Variables:**
   ```bash
   ANDY_GUARD_PI_THRESHOLD=0.5
   ANDY_GUARD_DEBERTA_MAX_LEN=512
   ANDY_GUARD_PI_ONNX_PATH=./onnx/codebert_model.onnx
   ANDY_GUARD_DEBERTA_VOCAB_PATH=./tokenizer/vocab.txt
   ANDY_GUARD_DEBERTA_MERGES_PATH=./tokenizer/merges.txt
   ANDY_GUARD_DEBERTA_CLS_ID=101
   ANDY_GUARD_DEBERTA_SEP_ID=102
   ANDY_GUARD_DEBERTA_PAD_ID=0
   ANDY_GUARD_DEBERTA_MASK_ID=103
   ANDY_GUARD_DEBERTA_UNK_ID=100
   ```

## Testing the Configuration

### 1. Test with Heuristics Only
```bash
# Set minimal configuration
set ANDY_GUARD_PI_THRESHOLD=0.5

# Run the API
dotnet run --project src/Andy.Guard.Api
```

### 2. Test the Scanner
```http
POST http://localhost:5000/api/prompt-scans
Content-Type: application/json

{
  "text": "exec('rm -rf /')",
  "scanners": ["codebert_security"],
  "options": { "threshold": 0.5 }
}
```

Expected response:
```json
{
  "decision": "Block",
  "score": 0.85,
  "highestSeverity": "High",
  "findings": [
    {
      "scanner": "codebert_security",
      "code": "DETECTED",
      "message": "Malicious code patterns detected",
      "severity": "High",
      "confidence": 0.85
    }
  ]
}
```

## Development Setup

### For Development (Heuristics Only)
Create a `.env` file or set environment variables:

```bash
# Windows
set ANDY_GUARD_PI_THRESHOLD=0.5

# Linux/macOS
export ANDY_GUARD_PI_THRESHOLD=0.5
```

### For Production (Full Model)
Ensure all environment variables are set in your deployment environment:

```bash
# Production environment variables
ANDY_GUARD_PI_THRESHOLD=0.7
ANDY_GUARD_DEBERTA_MAX_LEN=512
ANDY_GUARD_PI_ONNX_PATH=/app/models/codebert_model.onnx
ANDY_GUARD_DEBERTA_VOCAB_PATH=/app/tokenizer/vocab.txt
ANDY_GUARD_DEBERTA_MERGES_PATH=/app/tokenizer/merges.txt
ANDY_GUARD_DEBERTA_CLS_ID=101
ANDY_GUARD_DEBERTA_SEP_ID=102
ANDY_GUARD_DEBERTA_PAD_ID=0
ANDY_GUARD_DEBERTA_MASK_ID=103
ANDY_GUARD_DEBERTA_UNK_ID=100
```

## Troubleshooting

### Scanner Not Detecting Threats
- Check that `ANDY_GUARD_PI_THRESHOLD` is set appropriately (lower = more sensitive)
- Verify the scanner is registered: `"scanners": ["codebert_security"]`

### Tokenizer Errors
- Ensure `ANDY_GUARD_DEBERTA_VOCAB_PATH` and `ANDY_GUARD_DEBERTA_MERGES_PATH` point to valid files
- Check that the tokenizer files are compatible with CodeBERT

### Model Errors
- Verify `ANDY_GUARD_PI_ONNX_PATH` points to a valid ONNX model
- Ensure the model is compatible with the input format expected by CodeBERT

## Current Status

✅ **Scanner Implementation**: Complete
✅ **Heuristic Detection**: Working (no model required)
⏳ **Full Model Support**: Requires ONNX model and tokenizer files
✅ **API Integration**: Complete
✅ **Unit Tests**: All passing

The scanner will work with just the threshold environment variable set, using heuristic detection for immediate functionality.
