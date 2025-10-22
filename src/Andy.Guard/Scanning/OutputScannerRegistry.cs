using Andy.Guard.Scanning.Abstractions;

namespace Andy.Guard.Scanning;

/// <summary>
/// Default registry that discovers <see cref="IOutputScanner"/> via DI and runs selected scanners.
/// </summary>
public sealed class OutputScannerRegistry : IOutputScannerRegistry
{
    private readonly IReadOnlyDictionary<string, IOutputScanner> _scanners;

    public OutputScannerRegistry(IEnumerable<IOutputScanner> scanners)
    {
        _scanners = scanners.ToDictionary(s => s.Name, s => s, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<string> RegisteredOutputScanners => _scanners.Keys.ToArray();

    public async Task<IReadOnlyDictionary<string, ScanResult>> ScanAsync(
        string prompt,
        string output,
        IEnumerable<string>? scanners = null,
        ScanOptions? options = null)
    {
        IEnumerable<IOutputScanner> selectedScanners;

        if (scanners is null)
        {
            selectedScanners = _scanners.Values;
        }
        else
        {
            var scannerNames = scanners as string[] ?? scanners.ToArray();
            selectedScanners = scannerNames.Length == 0
                ? _scanners.Values
                : scannerNames.Where(_scanners.ContainsKey).Select(name => _scanners[name]);
        }

        var scannerArray = selectedScanners as IOutputScanner[] ?? selectedScanners.ToArray();
        if (scannerArray.Length == 0)
        {
            return new Dictionary<string, ScanResult>(StringComparer.OrdinalIgnoreCase);
        }

        var scanTasks = new (string Name, Task<ScanResult> Task)[scannerArray.Length];
        for (var i = 0; i < scannerArray.Length; i++)
        {
            var scanner = scannerArray[i];
            scanTasks[i] = (scanner.Name, scanner.ScanAsync(prompt, output, options));
        }

        await Task.WhenAll(scanTasks.Select(static tuple => tuple.Task));

        var scanResults = new Dictionary<string, ScanResult>(scannerArray.Length, StringComparer.OrdinalIgnoreCase);
        foreach (var (name, task) in scanTasks)
        {
            scanResults[name] = task.Result;
        }

        return scanResults;
    }
}
