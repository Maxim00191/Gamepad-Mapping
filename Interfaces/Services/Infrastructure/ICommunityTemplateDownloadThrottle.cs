using GamepadMapperGUI.Models;
using GamepadMapperGUI.Models.Core;

namespace GamepadMapperGUI.Interfaces.Services.Infrastructure;

/// <summary>
/// Client-side guard for community template downloads: spacing between attempts and a rolling cap on successful downloads per hour.
/// Intended to reduce accidental or abusive traffic to raw GitHub / CDN endpoints (configurable via <see cref="AppSettings"/>).
/// </summary>
public interface ICommunityTemplateDownloadThrottle
{
    /// <summary>
    /// Call before starting a network download. When this returns non-null, do not perform HTTP and do not call
    /// <see cref="RegisterSuccessfulDownload"/>.
    /// </summary>
    CommunityTemplateDownloadResult? TryBeginDownloadAttempt(AppSettings settings);

    /// <summary>Call after a download completed successfully (JSON parsed and saved).</summary>
    void RegisterSuccessfulDownload(AppSettings settings);
}
