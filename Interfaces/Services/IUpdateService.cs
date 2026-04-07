using System.Threading.Tasks;

namespace GamepadMapperGUI.Interfaces.Services;

public record AppUpdateInfo(string CurrentVersion, string? LatestVersion, string? ReleaseUrl, bool IsUpdateAvailable, string? ErrorMessage = null, bool IsForbidden = false);

public interface IUpdateService
{
    Task<AppUpdateInfo> CheckForUpdatesAsync(string owner, string repo, bool includePrereleases);
}
