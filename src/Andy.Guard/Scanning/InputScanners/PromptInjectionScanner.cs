using System.Diagnostics;
using Andy.Guard.Scanning;
using Andy.Guard.Scanning.Abstractions;
using Andy.Guard.Tokenizers.Deberta;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Andy.Guard.InputScanners;

/// <summary>
/// DeBERTa-backed prompt injection scanner.
///
/// Uses <see cref="DebertaTokenizer"/> for fast, HuggingFace-compatible preprocessing when configured
/// and falls back to a lightweight heuristic scorer otherwise. This design mirrors Protect AI's
/// prompt_injection LLM Guard approach: tokenize → classify → threshold. This scanners default to the model: protectai/deberta-v3-base-prompt-injection-v2.
/// <br></br>
/// <br>Configuration is read from environment variables to avoid hardcoding model specifics:</br>
/// <br>- <c>ANDY_GUARD_DEBERTA_SPM_PATH</c>: Path to the SentencePiece <c>spm.model</c></br>
/// <br>- <c>ANDY_GUARD_DEBERTA_MAX_LEN</c>: Max sequence length (default 512)</br>
/// <br>- <c>ANDY_GUARD_DEBERTA_CLS_ID</c>, <c>..._SEP_ID</c>, <c>..._PAD_ID</c>, <c>..._MASK_ID</c>, <c>..._UNK_ID</c></br>
/// <br>- <c>ANDY_GUARD_PI_THRESHOLD</c>: Probability threshold (default 0.5)</br>
/// <br></br>
/// <br>If the tokenizer can be created (env configured), encodings are produced and included in metadata.
/// Actual model inference can be plugged in via constructor injection of a scorer; otherwise a
/// high-recall heuristic is used to preserve behavior without external dependencies.</br>
/// </summary>
public class PromptInjectionScanner : IInputScanner
{
    private static readonly string[] HeuristicPhrases =
    {
        "ignore previous",
        "override",
        "system:",
        "act as",
        "disregard the rules"
    };

    private readonly DebertaTokenizer? _tokenizer;
    private readonly int _maxLen;
    private readonly float _threshold;
    private readonly Func<string, float>? _modelScore; // Optional external scorer (kept for flexibility)
    private readonly InferenceSession? _onnxSession;   // Self-contained inference when configured
    private readonly string _onnxInputIds = "input_ids";
    private readonly string _onnxAttentionMask = "attention_mask";
    private readonly string _onnxOutput = "logits";

    public PromptInjectionScanner(Func<DebertaEncoding, float>? modelScorer = null)
    {
        // Load config without hardcoding: prefer env, fall back to safe defaults where applicable
        _maxLen = ReadInt("ANDY_GUARD_DEBERTA_MAX_LEN", 512);
        _threshold = ReadFloat("ANDY_GUARD_PI_THRESHOLD", 0.5f);

        _tokenizer = TryCreateTokenizer(_maxLen);
        if (modelScorer is not null && _tokenizer is not null)
        {
            // Wrap provided scorer with tokenization-based encoding
            _modelScore = (text) => modelScorer(_tokenizer.Encode(text));
        }


        // Self-contained ONNX runtime path (optional). If not provided, try to download from Hugging Face.
        var onnxPath = ResolveOnnxModelPath();
        if (!string.IsNullOrWhiteSpace(onnxPath) && File.Exists(onnxPath))
        {
            try
            {
                // Use default SessionOptions for portability; consumers can tune EPs externally if needed.
                _onnxSession = new InferenceSession(onnxPath);

                // Optionally read dynamic names to be robust to exported graphs
                var inputNames = _onnxSession.InputNames;
                if (!inputNames.Contains(_onnxInputIds) && inputNames.Count > 0)
                    _onnxInputIds = inputNames[0];
                if (!inputNames.Contains(_onnxAttentionMask) && inputNames.Count > 1)
                    _onnxAttentionMask = inputNames[1];

                var outputNames = _onnxSession.OutputNames;
                if (!outputNames.Contains(_onnxOutput) && outputNames.Count > 0)
                    _onnxOutput = outputNames[0];
            }
            catch
            {
                _onnxSession = null;
            }
        }
    }

    public string Name => nameof(PromptInjectionScanner);

    public Task<ScanResult> ScanAsync(string text, ScanOptions? options = null)
        => Task.FromResult(Analyze(text, options));

    private ScanResult Analyze(string text, ScanOptions? options)
    {
        var sw = Stopwatch.StartNew();

        // Per-request overrides (do not hardcode)
        var threshold = options?.Threshold ?? _threshold;
        var maxLen = options?.MaxTokenLength ?? _maxLen;

        // Fast path: heuristic cue detection (very cheap, high recall)
        int cues = 0;
        foreach (var h in HeuristicPhrases)
        {
            if (text.Contains(h, StringComparison.OrdinalIgnoreCase))
                cues++;
        }

        // If a model scorer is available, use it; otherwise synthesize a probability from cues and length.
        float probability;
        Dictionary<string, object>? meta = null;

        if (_onnxSession is not null && _tokenizer is not null)
        {
            var enc = (maxLen == _maxLen) ? _tokenizer.Encode(text) : Reencode(text, maxLen);
            probability = ScoreWithOnnx(enc);
            meta = new Dictionary<string, object>
            {
                ["engine"] = "deberta_onnx",
                ["seq_len"] = enc.SequenceLength,
                ["tokenizer_max_len"] = maxLen,
                ["heuristic_cues"] = cues
            };
        }
        else if (_modelScore is not null && _tokenizer is not null)
        {
            var enc = (maxLen == _maxLen) ? _tokenizer.Encode(text) : Reencode(text, maxLen);
            probability = _modelScore(text);
            meta = new Dictionary<string, object>
            {
                ["engine"] = "deberta_model",
                ["seq_len"] = enc.SequenceLength,
                ["tokenizer_max_len"] = maxLen,
                ["heuristic_cues"] = cues
            };
        }
        else
        {
            // Heuristic-only fallback: scale probability based on cues and presence of admin-ish markers
            var adminHint = text.IndexOf("system:", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            text.IndexOf("you are", StringComparison.OrdinalIgnoreCase) >= 0;
            probability = Math.Clamp(0.15f + 0.25f * cues + (adminHint ? 0.2f : 0f), 0f, 0.98f);

            // Tokenization metadata (if available) without requiring inference
            if (_tokenizer is not null)
            {
                var enc = (maxLen == _maxLen) ? _tokenizer.Encode(text) : Reencode(text, maxLen);
                meta = new Dictionary<string, object>
                {
                    ["engine"] = "heuristics+tokenizer",
                    ["seq_len"] = enc.SequenceLength,
                    ["tokenizer_max_len"] = maxLen,
                    ["heuristic_cues"] = cues
                };
            }
            else
            {
                meta = new Dictionary<string, object>
                {
                    ["engine"] = "heuristics",
                    ["length"] = text.Length,
                    ["heuristic_cues"] = cues
                };
            }
        }

        var detected = probability >= threshold;
        var risk = detected
            ? (probability >= 0.85f ? RiskLevel.High : RiskLevel.Medium)
            : RiskLevel.Low;

        sw.Stop();
        return new ScanResult
        {
            IsThreatDetected = detected,
            ConfidenceScore = probability,
            RiskLevel = risk,
            Metadata = options?.IncludeMetadata == false ? null : meta,
            ProcessingTime = sw.Elapsed
        };
    }

    private float ScoreWithOnnx(DebertaEncoding enc)
    {
        if (_onnxSession is null)
            return 0f;

        // ONNX models typically expect Int64 tensors for BERT-family inputs
        var idsLong = new long[enc.InputIds.Length];
        for (int i = 0; i < enc.InputIds.Length; i++)
            idsLong[i] = enc.InputIds[i];

        var maskLong = new long[enc.AttentionMask.Length];
        for (int i = 0; i < enc.AttentionMask.Length; i++)
            maskLong[i] = enc.AttentionMask[i];

        var shape = new int[] { 1, enc.SequenceLength };
        var idsTensor = new DenseTensor<long>(idsLong, shape);
        var maskTensor = new DenseTensor<long>(maskLong, shape);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(_onnxInputIds, idsTensor),
            NamedOnnxValue.CreateFromTensor(_onnxAttentionMask, maskTensor)
        };

        using var results = _onnxSession.Run(inputs, new[] { _onnxOutput });
        var output = results.FirstOrDefault(r => r.Name == _onnxOutput) ?? results.First();
        var logitsTensor = output.AsTensor<float>();

        // Expect shape [1,2] for binary classification.
        // Compute softmax over last dim and return p(class=1).
        float l0 = logitsTensor.Length >= 1 ? logitsTensor.ToArray()[0] : 0f;
        float l1 = logitsTensor.Length >= 2 ? logitsTensor.ToArray()[1] : 0f;
        return Softmax2(l0, l1);
    }

    private static float Softmax2(float a, float b)
    {
        var m = Math.Max(a, b);
        var ea = Math.Exp(a - m);
        var eb = Math.Exp(b - m);
        return (float)(eb / (ea + eb + 1e-9));
    }

    private static int ReadInt(string env, int @default)
        => int.TryParse(Environment.GetEnvironmentVariable(env), out var v) ? v : @default;

    private static float ReadFloat(string env, float @default)
        => float.TryParse(Environment.GetEnvironmentVariable(env), out var v) ? v : @default;

    private static DebertaTokenizer? TryCreateTokenizer(int maxLen)
    {
        // All IDs must be provided to avoid hardcoding; if any are missing, skip tokenizer creation.
        var spmPath = Environment.GetEnvironmentVariable("ANDY_GUARD_DEBERTA_SPM_PATH");
        if (string.IsNullOrWhiteSpace(spmPath) || !File.Exists(spmPath))
            return null;

        bool Have(string name) => int.TryParse(Environment.GetEnvironmentVariable(name), out _);
        if (!(Have("ANDY_GUARD_DEBERTA_CLS_ID") &&
              Have("ANDY_GUARD_DEBERTA_SEP_ID") &&
              Have("ANDY_GUARD_DEBERTA_PAD_ID") &&
              Have("ANDY_GUARD_DEBERTA_MASK_ID") &&
              Have("ANDY_GUARD_DEBERTA_UNK_ID")))
        {
            return null;
        }

        var clsId = int.Parse(Environment.GetEnvironmentVariable("ANDY_GUARD_DEBERTA_CLS_ID")!);
        var sepId = int.Parse(Environment.GetEnvironmentVariable("ANDY_GUARD_DEBERTA_SEP_ID")!);
        var padId = int.Parse(Environment.GetEnvironmentVariable("ANDY_GUARD_DEBERTA_PAD_ID")!);
        var maskId = int.Parse(Environment.GetEnvironmentVariable("ANDY_GUARD_DEBERTA_MASK_ID")!);
        var unkId = int.Parse(Environment.GetEnvironmentVariable("ANDY_GUARD_DEBERTA_UNK_ID")!);

        try
        {
            return DebertaTokenizer.FromFile(spmPath!, clsId, sepId, padId, maskId, unkId, maxLen);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Resolve a usable ONNX model path by checking env, then attempting a Hugging Face download.
    /// Env vars:
    /// - ANDY_GUARD_PI_ONNX_PATH: absolute/relative path to a local .onnx file
    /// - ANDY_GUARD_PI_ONNX_HF_REPO: repo id (default: protectai/deberta-v3-base-prompt-injection-v2)
    /// - ANDY_GUARD_PI_ONNX_HF_REVISION: branch/tag/commit (default: main)
    /// - ANDY_GUARD_PI_ONNX_FILENAME: path inside the repo (default: onnx/model.onnx)
    /// - ANDY_GUARD_PI_ONNX_LOCAL_PATH: where to place the downloaded file (default: ./onnx/model.onnx)
    /// </summary>
    private static string? ResolveOnnxModelPath()
    {
        // 1) If explicit path provided and exists, use it.
        var explicitPath = Environment.GetEnvironmentVariable("ANDY_GUARD_PI_ONNX_PATH");
        if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath))
            return explicitPath;

        // 2) Attempt download from Hugging Face if repo is specified (or use default).
        var repo = Environment.GetEnvironmentVariable("ANDY_GUARD_PI_ONNX_HF_REPO");
        if (string.IsNullOrWhiteSpace(repo))
            repo = "protectai/deberta-v3-base-prompt-injection-v2";

        var revision = Environment.GetEnvironmentVariable("ANDY_GUARD_PI_ONNX_HF_REVISION");
        if (string.IsNullOrWhiteSpace(revision))
            revision = "main";

        var fileInRepo = Environment.GetEnvironmentVariable("ANDY_GUARD_PI_ONNX_FILENAME");
        if (string.IsNullOrWhiteSpace(fileInRepo))
            fileInRepo = "onnx/model.onnx";

        // Default local target: app output folder ./onnx/model.onnx
        var baseDir = AppContext.BaseDirectory;
        var localDefault = Path.Combine(baseDir, "onnx", "model.onnx");
        var localTarget = Environment.GetEnvironmentVariable("ANDY_GUARD_PI_ONNX_LOCAL_PATH");
        if (string.IsNullOrWhiteSpace(localTarget))
            localTarget = localDefault;

        // If already downloaded, use it
        if (File.Exists(localTarget))
            return localTarget;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(localTarget)!);
            var url = $"https://huggingface.co/{repo}/resolve/{revision}/{fileInRepo}?download=true";

            using var http = new HttpClient();
            using var resp = http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
            if (!resp.IsSuccessStatusCode)
                return null;

            using var src = resp.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
            using var dst = File.Create(localTarget);
            src.CopyTo(dst);
            return localTarget;
        }
        catch
        {
            return null;
        }
    }

    private DebertaEncoding Reencode(string text, int maxLen)
    {
        // Recreate a tokenizer with a different maxLen only if necessary; keep same IDs from env.
        var spmPath = Environment.GetEnvironmentVariable("ANDY_GUARD_DEBERTA_SPM_PATH");
        if (_tokenizer is null || string.IsNullOrWhiteSpace(spmPath))
            return new DebertaEncoding { InputIds = Array.Empty<int>(), AttentionMask = Array.Empty<int>() };

        var clsId = int.Parse(Environment.GetEnvironmentVariable("ANDY_GUARD_DEBERTA_CLS_ID")!);
        var sepId = int.Parse(Environment.GetEnvironmentVariable("ANDY_GUARD_DEBERTA_SEP_ID")!);
        var padId = int.Parse(Environment.GetEnvironmentVariable("ANDY_GUARD_DEBERTA_PAD_ID")!);
        var maskId = int.Parse(Environment.GetEnvironmentVariable("ANDY_GUARD_DEBERTA_MASK_ID")!);
        var unkId = int.Parse(Environment.GetEnvironmentVariable("ANDY_GUARD_DEBERTA_UNK_ID")!);
        using var fresh = DebertaTokenizer.FromFile(spmPath!, clsId, sepId, padId, maskId, unkId, maxLen);
        return fresh.Encode(text);
    }
}
