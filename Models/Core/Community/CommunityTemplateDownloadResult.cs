namespace GamepadMapperGUI.Models.Core;

/// <summary>Outcome of downloading a community template from the catalog.</summary>
public enum CommunityTemplateDownloadThrottleReason
{
    None = 0,
    /// <summary>Minimum spacing between download attempts was not met.</summary>
    MinIntervalBetweenDownloads = 1,
    /// <summary>Rolling hourly cap on successful downloads was reached.</summary>
    HourlyDownloadQuota = 2,
}

/// <param name="Success">True when the template JSON was fetched and saved.</param>
/// <param name="ThrottleReason">Set when <paramref name="Success"/> is false because of client-side throttling.</param>
/// <param name="RetryAfterSeconds">Suggested wait before retry when throttled; 0 otherwise.</param>
public readonly record struct CommunityTemplateDownloadResult(
    bool Success,
    CommunityTemplateDownloadThrottleReason ThrottleReason = CommunityTemplateDownloadThrottleReason.None,
    int RetryAfterSeconds = 0);
