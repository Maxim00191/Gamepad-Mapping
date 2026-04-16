namespace GamepadMapperGUI.Models.Core;

public sealed record CommunityTemplateUploadResult(
    bool Success,
    string? PullRequestHtmlUrl,
    string? ErrorMessage,
    bool IsPipelineBusy = false);
