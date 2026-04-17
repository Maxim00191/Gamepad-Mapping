using System;
using System.Collections.Generic;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Models.Core;

namespace GamepadMapperGUI.Services.Infrastructure;

/// <summary>
/// Thread-safe in-process throttle for community template downloads. Policy is read from <see cref="AppSettings"/> on each call
/// so live settings changes apply without restarting the app.
/// </summary>
public sealed class CommunityTemplateDownloadThrottle : ICommunityTemplateDownloadThrottle
{
    private const int MinIntervalSecondsMax = 600;
    private const int HourlyQuotaMax = 500;

    private readonly object _sync = new();
    private DateTime? _lastDownloadAttemptUtc;
    private readonly Queue<DateTime> _successfulDownloadUtcTimes = new();

    public CommunityTemplateDownloadResult? TryBeginDownloadAttempt(AppSettings settings)
    {
        var now = DateTime.UtcNow;
        var minInterval = Math.Clamp(settings.CommunityTemplateDownloadMinIntervalSeconds, 0, MinIntervalSecondsMax);
        var maxPerHour = Math.Clamp(settings.CommunityTemplateDownloadMaxPerHour, 0, HourlyQuotaMax);

        lock (_sync)
        {
            PruneOlderThan(_successfulDownloadUtcTimes, now.AddHours(-1));

            if (maxPerHour > 0 && _successfulDownloadUtcTimes.Count >= maxPerHour)
            {
                var oldest = _successfulDownloadUtcTimes.Peek();
                var retryAfter = (int)Math.Ceiling((oldest.AddHours(1) - now).TotalSeconds);
                if (retryAfter < 1)
                    retryAfter = 1;
                return new CommunityTemplateDownloadResult(
                    false,
                    CommunityTemplateDownloadThrottleReason.HourlyDownloadQuota,
                    retryAfter);
            }

            if (minInterval > 0 && _lastDownloadAttemptUtc is { } last)
            {
                var elapsed = (now - last).TotalSeconds;
                if (elapsed < minInterval)
                {
                    var retryAfter = (int)Math.Ceiling(minInterval - elapsed);
                    if (retryAfter < 1)
                        retryAfter = 1;
                    return new CommunityTemplateDownloadResult(
                        false,
                        CommunityTemplateDownloadThrottleReason.MinIntervalBetweenDownloads,
                        retryAfter);
                }
            }

            _lastDownloadAttemptUtc = now;
            return null;
        }
    }

    public void RegisterSuccessfulDownload(AppSettings settings)
    {
        var now = DateTime.UtcNow;
        var maxPerHour = Math.Clamp(settings.CommunityTemplateDownloadMaxPerHour, 0, HourlyQuotaMax);

        lock (_sync)
        {
            PruneOlderThan(_successfulDownloadUtcTimes, now.AddHours(-1));
            if (maxPerHour <= 0)
                return;
            _successfulDownloadUtcTimes.Enqueue(now);
        }
    }

    private static void PruneOlderThan(Queue<DateTime> q, DateTime cutoffUtc)
    {
        while (q.Count > 0 && q.Peek() < cutoffUtc)
            q.Dequeue();
    }
}
