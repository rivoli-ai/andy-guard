using Andy.Guard.InputScanners;
using Andy.Guard.Scanning;
using Andy.Guard.Scanning.Abstractions;

namespace Andy.Guard.Scanning;

/// <summary>
/// Default registry that discovers <see cref="ITextScanner"/> via DI and runs selected scanners.
/// </summary>
public sealed class ScannerRegistry : IScannerRegistry
{
    private readonly IReadOnlyDictionary<string, ITextScanner> _scanners;

    public ScannerRegistry(IEnumerable<ITextScanner> scanners)
    {
        _scanners = scanners.ToDictionary(s => s.Name, s => s, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<string> RegisteredScanners => _scanners.Keys.ToArray();

    public async Task<IReadOnlyDictionary<string, ScanResult>> ScanAsync(
        ScanTarget target,
        string text,
        IEnumerable<string>? scanners = null,
        ScanOptions? options = null)
    {
        var selected = (scanners is null || !scanners.Any())
            ? _scanners.Values
            : scanners.Where(name => _scanners.ContainsKey(name)).Select(name => _scanners[name]);

        var result = new Dictionary<string, ScanResult>(StringComparer.OrdinalIgnoreCase);
        foreach (var scanner in selected)
        {
            if (!scanner.SupportsTarget(target))
                continue;

            var scan = await scanner.ScanAsync(target, text, options);
            result[scanner.Name] = scan;
        }

        return result;
    }
}
