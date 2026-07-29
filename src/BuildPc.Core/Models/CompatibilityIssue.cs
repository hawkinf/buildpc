namespace BuildPc.Core.Models;

public enum IssueSeverity
{
    Information,
    Warning,
    Error
}

public sealed record CompatibilityIssue(IssueSeverity Severity, string Message);
