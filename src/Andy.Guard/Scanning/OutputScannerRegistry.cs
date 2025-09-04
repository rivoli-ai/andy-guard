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
        var selected = (scanners is null || !scanners.Any())
            ? _scanners.Values
            : scanners.Where(_scanners.ContainsKey).Select(name => _scanners[name]);

        var result = new Dictionary<string, ScanResult>(StringComparer.OrdinalIgnoreCase);
        foreach (var scanner in selected)
        {
            var scan = await scanner.ScanAsync(prompt, output, options);
            result[scanner.Name] = scan;
        }

        return result;
    }
}

