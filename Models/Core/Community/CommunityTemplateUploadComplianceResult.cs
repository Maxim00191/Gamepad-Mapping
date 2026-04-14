namespace GamepadMapperGUI.Models.Core.Community;

public enum CommunityTemplateComplianceSeverity
{
    Ok,
    Warning,
    Error
}

public sealed record CommunityTemplateComplianceIssue(
    string TemplateLabel,
    string Detail,
    string? SuggestionKey);

public sealed record CommunityTemplateComplianceStepResult(
    string TitleKey,
    string PromptKey,
    CommunityTemplateComplianceSeverity Severity,
    IReadOnlyList<CommunityTemplateComplianceIssue> Issues);

public sealed record CommunityTemplateUploadComplianceResult(
    bool ReadyToSubmit,
    IReadOnlyList<CommunityTemplateComplianceStepResult> Steps);
