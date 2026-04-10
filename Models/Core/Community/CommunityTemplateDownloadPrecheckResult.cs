namespace GamepadMapperGUI.Models.Core;

public sealed record CommunityTemplateDownloadPrecheckResult(
    bool HasSameFolderIdAndName,
    string? ExistingDisplayName);
