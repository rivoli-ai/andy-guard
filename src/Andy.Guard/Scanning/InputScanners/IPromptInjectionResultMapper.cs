using System;
using Andy.Guard.Scanning;

namespace Andy.Guard.InputScanners;

public interface IPromptInjectionResultMapper
{
    ScanResult Map(RemoteResponse remote, ScanOptions? options, TimeSpan processingTime);
}
