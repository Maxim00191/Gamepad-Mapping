namespace GamepadMapperGUI.Models.Core;

public record AppUpdateInfo(
    string CurrentVersion,
    string? LatestVersion,
    string? ReleaseUrl,
    bool IsUpdateAvailable,
    string? ErrorMessage = null,
    bool IsForbidden = false);
