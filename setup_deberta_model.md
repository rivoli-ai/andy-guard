# 🧱 Prerequisites to Install and Run the Model

## 1️⃣ Install a Supported Python Version

Make sure you have a Python version compatible with **Transformers**, **PyTorch**, and **Accelerate**.  
Recommended version: **Python 3.13.7** → [Download here](https://www.python.org/downloads/release/python-3137/)

### 🧩 Compatibility Summary

| Library | Minimum Version | Python 3.13 Support | Notes |
|----------|-----------------|--------------------|--------|
| **PyTorch** | `torch >= 2.5.0` | ✅ Yes | Official wheels for Python 3.13 available since 2.5.0 — use CPU or CUDA builds from [pytorch.org/whl](https://download.pytorch.org/whl/) |
| **Transformers** | `transformers >= 4.46.0` | ✅ Yes | Fully compatible with Python 3.13 and PyTorch 2.5 |
| **Accelerate** | `accelerate >= 1.0.0` | ✅ Yes | Optional helper for performance and multi-device inference |

---

## 2️⃣ Add Python to Environment Variables (Windows)

If Python isn’t recognized in PowerShell, add it to your environment variables:

```powershell
$pythonPath = "$env:LOCALAPPDATA\Programs\Python\Python313"
[System.Environment]::SetEnvironmentVariable("Path", $env:Path + ";$pythonPath;$pythonPath\Scripts", "User")
```

Then close and reopen PowerShell, and verify:

```powershell
python -V
pip --version
```

---

## 3️⃣ Upgrade `pip`

Ensure you have the latest package manager version:

```powershell
python -m pip install --upgrade pip
```

---

## 4️⃣ Install Required Libraries

Install the core dependencies:

```powershell
python -m pip install "torch>=2.5" "transformers>=4.46" accelerate
```

This installs:
- **PyTorch** → deep learning engine  
- **Transformers** → Hugging Face model API  
- **Accelerate** → optional performance/device manager  

> 💡 **GPU support:**  
> For NVIDIA GPUs, install the CUDA version of PyTorch:
> ```powershell
> python -m pip install torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cu121
> ```

---

## 5️⃣ Clone the Model Repository

Download the ONNX-ready DeBERTa model directly from Hugging Face:

```powershell
git clone https://huggingface.co/protectai/deberta-v3-base-injection-onnx.git
```

This will create a local folder containing:
```
deberta-v3-base-injection-onnx/
 ├─ model.onnx
 ├─ config.json
 ├─ tokenizer.json
 ├─ vocab.json
 └─ merges.txt
```

---

## 6️⃣ Configure Environment Variables (ONNX Model Support)

Set the following environment variables in **PowerShell** to enable ONNX integration:

```powershell
setx ANDY_GUARD_PI_ONNX_PATH "C:\dev\Samples\onnx-deberta-v3-base\model.onnx"
setx ANDY_GUARD_DEBERTA_SPM_PATH "C:\dev\Samples\onnx-deberta-v3-base"
setx ANDY_GUARD_DEBERTA_CLS_ID 0
setx ANDY_GUARD_DEBERTA_SEP_ID 2
setx ANDY_GUARD_DEBERTA_PAD_ID 1
setx ANDY_GUARD_DEBERTA_MASK_ID 3
setx ANDY_GUARD_DEBERTA_UNK_ID 100
```

> ⚠️ After setting variables, **close and reopen** your terminal for the changes to take effect.

---

## 7️⃣ Run the API

Start the .NET API locally:

```powershell
dotnet run --project .\src\Andy.Guard.Api\Andy.Guard.Api.csproj --urls http://localhost:5000
```

You should see output similar to:

```
Now listening on: http://localhost:5000
Application started. Press Ctrl+C to shut down.
```

✅ The API is now running and connected to your local ONNX-based DeBERTa model.
