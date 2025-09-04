namespace Andy.Guard.Scanning.Abstractions;

public interface IOutputScanner
{
    /// <summary>
    /// Canonical scanner name
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Scans output of the model and returns sanitized output with a flag indicating if it is valid or malicious.
    /// </summary>
    Task<ScanResult> ScanAsync(string prompt, string output, ScanOptions? options = null);
}
