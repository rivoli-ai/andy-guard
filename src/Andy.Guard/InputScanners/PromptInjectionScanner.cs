using Andy.Guard.InputScanners.Abstractions;

namespace Andy.Guard.InputScanners;

// NOTE: Placeholder stub. Replace internally with a DeBERTa model-backed implementation.
public class PromptInjectionScanner : IPromptInjectionScanner
{
    private static readonly string[] Heuristics =
    {
        "ignore previous",
        "override",
        "system:",
        "act as",
        "disregard the rules"
    };

    public Task<ScanResult> ScanInputAsync(string input, ScanOptions? options = null)
        => Task.FromResult(Analyze(input));

    public Task<ScanResult> ScanOutputAsync(string output, ScanOptions? options = null)
        => Task.FromResult(Analyze(output));

    public Task<IEnumerable<ScanResult>> ScanInputBatchAsync(IEnumerable<string> inputs)
        => Task.FromResult(inputs.Select(Analyze) as IEnumerable<ScanResult>);

    public Task<IEnumerable<ScanResult>> ScanOutputBatchAsync(IEnumerable<string> outputs)
        => Task.FromResult(outputs.Select(Analyze) as IEnumerable<ScanResult>);

    private static ScanResult Analyze(string text)
    {
        var detected = Heuristics.Any(h => text.Contains(h, StringComparison.OrdinalIgnoreCase));
        return new ScanResult
        {
            IsInjectionDetected = detected,
            ConfidenceScore = detected ? 0.8f : 0.2f,
            RiskLevel = detected ? RiskLevel.Medium : RiskLevel.Low,
            Metadata = new Dictionary<string, object>
            {
                ["length"] = text.Length,
                ["heuristics"] = detected ? Heuristics : Array.Empty<string>()
            },
            ProcessingTime = TimeSpan.FromMilliseconds(1)
        };
    }
}
