namespace Andy.Guard.InputScanners.Abstractions;

public interface IPromptInjectionScanner
{
    // Scan input before sending to LLM
    Task<ScanResult> ScanInputAsync(string prompt, ScanOptions? options = null);

    // Scan output received from LLM
    Task<ScanResult> ScanOutputAsync(string response, ScanOptions? options = null);

    // Batch processing for efficiency
    Task<IEnumerable<ScanResult>> ScanInputBatchAsync(IEnumerable<string> prompts);

    Task<IEnumerable<ScanResult>> ScanOutputBatchAsync(IEnumerable<string> responses);
}
