using Andy.Guard.Scanning.Abstractions;

namespace Andy.Guard.Scanning;

/// <summary>
/// Default registry that discovers <see cref="IInputScanner"/> via DI and runs selected scanners.
/// </summary>
public sealed class InputScannerRegistry : IInputScannerRegistry
{
    private readonly IReadOnlyDictionary<string, IInputScanner> _scanners;

    public InputScannerRegistry(IEnumerable<IInputScanner> scanners)
    {
        _scanners = scanners.ToDictionary(s => s.Name, s => s, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<string> RegisteredScanners => _scanners.Keys.ToArray();

    public async Task<IReadOnlyDictionary<string, ScanResult>> ScanAsync(
        string prompt,
        IEnumerable<string>? scanners = null,
        ScanOptions? options = null)
    {
        var selected = (scanners is null || !scanners.Any())
            ? _scanners.Values
            : scanners.Where(_scanners.ContainsKey).Select(name => _scanners[name]);

        var result = new Dictionary<string, ScanResult>(StringComparer.OrdinalIgnoreCase);
        foreach (var scanner in selected)
        {
            var scan = await scanner.ScanAsync(prompt, options);
            result[scanner.Name] = scan;
        }

        return result;
    }
}
