using System.Threading;
using System.Threading.Tasks;
using GamepadMapperGUI.Models.Core;

namespace GamepadMapperGUI.Interfaces.Services;

public record AppUpdateInfo(string CurrentVersion, string? LatestVersion, string? ReleaseUrl, bool IsUpdateAvailable, string? ErrorMessage = null, bool IsForbidden = false);

public interface IUpdateService
{
    Task<AppUpdateInfo> CheckForUpdatesAsync(string owner, string repo, bool includePrereleases);
    Task<ReleaseResolutionResult> ResolveReleaseAssetAsync(string owner, string repo, bool includePrereleases, AppInstallMode installMode, CancellationToken cancellationToken = default);
    Task DownloadReleaseAssetAsync(string assetDownloadUrl, string destinationFilePath, IProgress<ReleaseDownloadProgress>? progress = null, CancellationToken cancellationToken = default);
    string? ConsumeLastNetworkFallbackNotice();
}
