using Andy.Guard.InputScanners.Abstractions;

namespace Andy.Guard.InputScanners;

// TODO Implement this using the DebertaTokenizer to detect prompt injections
// 1. Ensure the scanner follows the implementation of the popular prompt_injection scanner of https://github.com/protectai/llm-guard
// 2. Attempt using the fine-tuned model from protectai/deberta-v3-base-prompt-injection
// 3. If the model is proprietary and not usable for commerical use, revert to the base model microsoft/deberta-v3-base
public class PromptInjectionScanner : IPromptInjectionScanner
{
    public Task<ScanResult> ScanInputAsync(string input, ScanOptions? options)
    {
        throw new NotImplementedException();
    }

    public Task<ScanResult> ScanOutputAsync(string output, ScanOptions? options)
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<ScanResult>> ScanInputBatchAsync(IEnumerable<string> inputs)
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<ScanResult>> ScanOutputBatchAsync(IEnumerable<string> outputs)
    {
        throw new NotImplementedException();
    }
}
