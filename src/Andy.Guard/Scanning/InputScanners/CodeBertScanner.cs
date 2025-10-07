using System.Diagnostics;
using Andy.Guard.Scanning;
using Andy.Guard.Scanning.Abstractions;
using Andy.Guard.Tokenizers.CodeBert;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Andy.Guard.InputScanners;

/// <summary>
/// CodeBERT-based scanner for detecting malicious code patterns and vulnerabilities.
/// Uses Microsoft's CodeBERT model for code understanding and security analysis.
/// </summary>
public class CodeBertScanner : IInputScanner
{
    private readonly float _threshold;
    private readonly int _maxLen;
    private readonly InferenceSession? _onnxSession;
    private readonly CodeBertTokenizer? _tokenizer;
    private readonly string _onnxInputIds = "input_ids";
    private readonly string _onnxAttentionMask = "attention_mask";
    private readonly string _onnxOutput = "logits";

    // CodeBERT-specific threat patterns
    private static readonly string[] CodeThreatPatterns =
    {
        "exec(",
        "eval(",
        "system(",
        "shell_exec",
        "__import__",
        "subprocess",
        "Process.Start",
        "Runtime.getRuntime",
        "new ProcessBuilder",
        "dangerouslySetInnerHTML",
        "innerHTML",
        "document.write",
        "Function(",
        "setTimeout(",
        "setInterval(",
        // Network attack patterns
        "XMLHttpRequest",
        "fetch(",
        "http://",
        "https://",
        "document.cookie",
        "localStorage",
        "sessionStorage",
        // SQL injection patterns
        "DROP TABLE",
        "DELETE FROM",
        "UPDATE SET",
        "INSERT INTO",
        "UNION SELECT",
        "--",
        "/*",
        "*/",
        // File system patterns
        "rm -rf",
        "del ",
        "format ",
        "chmod",
        "chown",
        "sudo",
        "su -",
        // Python file operations
        "open(",
        ".write(",
        ".read(",
        "os.remove",
        "os.unlink",
        "shutil.rmtree",
        "os.system",
        "subprocess.call",
        "subprocess.run",
        // File paths
        "/etc/passwd",
        "/etc/shadow",
        "C:\\",
        "C:/",
        "*.exe",
        "*.dll",
        "*.sys",
        // Windows file operations
        "del /f",
        "rd /s",
        "format c:",
        "attrib -r",
        "icacls",
        "takeown",
        // Authentication bypass
        "admin=true",
        "authenticated=true",
        "password=",
        "token=",
        "secret=",
        "key="
    };

    public CodeBertScanner()
    {
        // Use DeBERTa environment variables for compatibility
        _threshold = ReadFloat("ANDY_GUARD_PI_THRESHOLD", 0.5f);
        _maxLen = ReadInt("ANDY_GUARD_DEBERTA_MAX_LEN", 512);
        
        // Initialize tokenizer if available (using DeBERTa config)
        _tokenizer = TryCreateTokenizer();
        
        // Initialize ONNX session if model is available
        var onnxPath = ResolveCodeBertModelPath();
        if (!string.IsNullOrWhiteSpace(onnxPath) && File.Exists(onnxPath))
        {
            try
            {
                _onnxSession = new InferenceSession(onnxPath);
                
                // Configure input/output names dynamically
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

    public string Name => "codebert_security";

    public Task<ScanResult> ScanAsync(string code, ScanOptions? options = null)
        => Task.FromResult(Analyze(code, options));

    private ScanResult Analyze(string code, ScanOptions? options)
    {
        var sw = Stopwatch.StartNew();
        
        var threshold = options?.Threshold ?? _threshold;
        var maxLen = options?.MaxTokenLength ?? _maxLen;

        // Fast heuristic analysis for code threats
        int threatCues = 0;
        foreach (var pattern in CodeThreatPatterns)
        {
            if (code.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                threatCues++;
        }

        float probability;
        Dictionary<string, object>? meta = null;

        if (_onnxSession is not null)
        {
            // Use CodeBERT model for sophisticated analysis
            probability = ScoreWithCodeBert(code, maxLen);
            meta = new Dictionary<string, object>
            {
                ["engine"] = "codebert_onnx",
                ["threat_cues"] = threatCues,
                ["max_len"] = maxLen
            };
        }
        else
        {
            // Heuristic-only fallback - more aggressive detection
            var suspiciousKeywords = CountSuspiciousKeywords(code);
            var complexityScore = CalculateCodeComplexity(code);
            
            // More aggressive scoring for threat detection
            probability = Math.Clamp(
                0.2f + 0.4f * threatCues + 0.3f * suspiciousKeywords + 0.1f * complexityScore, 
                0f, 0.95f);

            meta = new Dictionary<string, object>
            {
                ["engine"] = "heuristics",
                ["threat_cues"] = threatCues,
                ["suspicious_keywords"] = suspiciousKeywords,
                ["complexity_score"] = complexityScore,
                ["length"] = code.Length
            };
        }

        var detected = probability >= threshold;
        var risk = detected
            ? (probability >= 0.8f ? RiskLevel.High : RiskLevel.Medium)
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

    private float ScoreWithCodeBert(string code, int maxLen)
    {
        if (_onnxSession is null || _tokenizer is null)
            return 0f;

        try
        {
            // Use proper CodeBERT tokenization
            var encoding = _tokenizer.Encode(code);
            
            // Create input tensors
            var inputIds = new long[encoding.InputIds.Length];
            var attentionMask = new long[encoding.AttentionMask.Length];
            
            for (int i = 0; i < encoding.InputIds.Length; i++)
            {
                inputIds[i] = encoding.InputIds[i];
                attentionMask[i] = encoding.AttentionMask[i];
            }

            var shape = new int[] { 1, encoding.InputIds.Length };
            var idsTensor = new DenseTensor<long>(inputIds, shape);
            var maskTensor = new DenseTensor<long>(attentionMask, shape);

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(_onnxInputIds, idsTensor),
                NamedOnnxValue.CreateFromTensor(_onnxAttentionMask, maskTensor)
            };

            using var results = _onnxSession.Run(inputs, new[] { _onnxOutput });
            var output = results.FirstOrDefault(r => r.Name == _onnxOutput) ?? results.First();
            var logitsTensor = output.AsTensor<float>();

            // Binary classification: [safe, malicious]
            float safeScore = logitsTensor.Length >= 1 ? logitsTensor.ToArray()[0] : 0f;
            float maliciousScore = logitsTensor.Length >= 2 ? logitsTensor.ToArray()[1] : 0f;
            
            return Softmax(maliciousScore, safeScore);
        }
        catch
        {
            return 0f;
        }
    }

    private static float Softmax(float a, float b)
    {
        var m = Math.Max(a, b);
        var ea = Math.Exp(a - m);
        var eb = Math.Exp(b - m);
        return (float)(ea / (ea + eb + 1e-9));
    }

    private CodeBertTokenizer? TryCreateTokenizer()
    {
        // Use DeBERTa environment variables for tokenizer paths
        var vocabPath = Environment.GetEnvironmentVariable("ANDY_GUARD_DEBERTA_VOCAB_PATH");
        var mergesPath = Environment.GetEnvironmentVariable("ANDY_GUARD_DEBERTA_MERGES_PATH");
        
        if (string.IsNullOrWhiteSpace(vocabPath) || string.IsNullOrWhiteSpace(mergesPath) ||
            !File.Exists(vocabPath) || !File.Exists(mergesPath))
            return null;

        try
        {
            // Use DeBERTa special token IDs (same as BERT/CodeBERT)
            var clsId = ReadInt("ANDY_GUARD_DEBERTA_CLS_ID", 101);
            var sepId = ReadInt("ANDY_GUARD_DEBERTA_SEP_ID", 102);
            var padId = ReadInt("ANDY_GUARD_DEBERTA_PAD_ID", 0);
            var maskId = ReadInt("ANDY_GUARD_DEBERTA_MASK_ID", 103);
            var unkId = ReadInt("ANDY_GUARD_DEBERTA_UNK_ID", 100);

            return CodeBertTokenizer.FromFiles(vocabPath, mergesPath, clsId, sepId, padId, maskId, unkId, _maxLen);
        }
        catch
        {
            return null;
        }
    }

    private int CountSuspiciousKeywords(string code)
    {
        var suspiciousWords = new[] { "password", "secret", "key", "token", "auth", "admin", "root" };
        return suspiciousWords.Count(word => code.Contains(word, StringComparison.OrdinalIgnoreCase));
    }

    private float CalculateCodeComplexity(string code)
    {
        // Simple complexity metric based on nesting and special characters
        var nesting = code.Count(c => c == '{' || c == '(' || c == '[');
        var specialChars = code.Count(c => "!@#$%^&*()_+-=[]{}|;':\",./<>?".Contains(c));
        return Math.Min(1.0f, (nesting + specialChars) / 100.0f);
    }

    private static string? ResolveCodeBertModelPath()
    {
        // Use DeBERTa environment variables for model path
        var localPath = Environment.GetEnvironmentVariable("ANDY_GUARD_PI_ONNX_PATH");
        if (!string.IsNullOrWhiteSpace(localPath) && File.Exists(localPath))
            return localPath;

        // Try default location for CodeBERT model
        var defaultPath = Path.Combine(AppContext.BaseDirectory, "onnx", "codebert_model.onnx");
        if (File.Exists(defaultPath))
            return defaultPath;

        return null;
    }

    private static int ReadInt(string env, int @default)
        => int.TryParse(Environment.GetEnvironmentVariable(env), out var v) ? v : @default;

    private static float ReadFloat(string env, float @default)
        => float.TryParse(Environment.GetEnvironmentVariable(env), out var v) ? v : @default;
}
