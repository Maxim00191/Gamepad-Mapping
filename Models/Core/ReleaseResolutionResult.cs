namespace GamepadMapperGUI.Models.Core;

public record ReleaseResolutionResult(
    string? VersionTag,
    string? ReleasePageUrl,
    ReleaseAssetInfo? MatchedAsset,
    string? ErrorMessage = null);
