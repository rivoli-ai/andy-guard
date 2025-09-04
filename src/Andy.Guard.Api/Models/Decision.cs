namespace Andy.Guard.Api.Models;

/// <summary>
/// Overall decision after aggregating scanner findings.
/// </summary>
public enum Decision
{
    Allow = 0,
    Review = 1,
    Block = 2
}

