namespace Gamepad_Mapping.ViewModels;

public sealed class CommunityTemplateComplianceIssueViewModel(string line, string? suggestion)
{
    public string Line { get; } = line;

    public string? Suggestion { get; } = suggestion;
}
