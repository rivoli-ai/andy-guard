namespace Andy.Guard.Api.Models;

/// <summary>
/// Indicates whether we scan a user input before the LLM or an LLM output.
/// </summary>
public enum ScanTarget
{
    Input = 0,
    Output = 1
}

