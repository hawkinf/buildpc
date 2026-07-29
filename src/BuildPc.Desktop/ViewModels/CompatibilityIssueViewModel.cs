using BuildPc.Core.Models;

namespace BuildPc.Desktop.ViewModels;

public sealed class CompatibilityIssueViewModel
{
    public CompatibilityIssueViewModel(CompatibilityIssue issue)
    {
        Message = issue.Message;
        Icon = issue.Severity switch
        {
            IssueSeverity.Error => "Error",
            IssueSeverity.Warning => "Warning",
            _ => "Check"
        };
        IsError = issue.Severity == IssueSeverity.Error;
        IsWarning = issue.Severity == IssueSeverity.Warning;
        IsSuccess = issue.Severity is not IssueSeverity.Error and not IssueSeverity.Warning;
    }

    public string Message { get; }
    public string Icon { get; }
    public bool IsError { get; }
    public bool IsWarning { get; }
    public bool IsSuccess { get; }
}
