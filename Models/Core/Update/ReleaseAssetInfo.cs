namespace GamepadMapperGUI.Models.Core;

public record ReleaseAssetInfo(
    string Name,
    string DownloadUrl,
    long? SizeBytes,
    string? Sha256 = null);
