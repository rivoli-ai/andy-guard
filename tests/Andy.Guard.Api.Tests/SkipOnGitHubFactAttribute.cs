using System;
using Xunit;

namespace Andy.Guard.Api.Tests;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class SkipOnGitHubFactAttribute : FactAttribute
{
    public SkipOnGitHubFactAttribute(string? skipReason = null)
    {
        if (IsRunningOnGitHubActions())
            Skip = skipReason ?? "Skipped when running on GitHub Actions.";
    }

    private static bool IsRunningOnGitHubActions()
    {
        var value = Environment.GetEnvironmentVariable("GITHUB_ACTIONS");
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }
}
