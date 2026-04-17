using GamepadMapperGUI.Models;
using GamepadMapperGUI.Models.Core;
using GamepadMapperGUI.Services.Infrastructure;
using Xunit;

namespace GamepadMapping.Tests.Services;

public class CommunityTemplateDownloadThrottleTests
{
    [Fact]
    public void TryBegin_AllowsFirstAttempt_AndBlocksWithinMinInterval()
    {
        var throttle = new CommunityTemplateDownloadThrottle();
        var settings = new AppSettings
        {
            CommunityTemplateDownloadMinIntervalSeconds = 10,
            CommunityTemplateDownloadMaxPerHour = 0
        };

        Assert.Null(throttle.TryBeginDownloadAttempt(settings));
        var blocked = throttle.TryBeginDownloadAttempt(settings);
        Assert.NotNull(blocked);
        Assert.Equal(CommunityTemplateDownloadThrottleReason.MinIntervalBetweenDownloads, blocked!.Value.ThrottleReason);
        Assert.True(blocked.Value.RetryAfterSeconds >= 1);
    }

    [Fact]
    public void RegisterSuccessful_EnforcesHourlyCap()
    {
        var throttle = new CommunityTemplateDownloadThrottle();
        var settings = new AppSettings
        {
            CommunityTemplateDownloadMinIntervalSeconds = 0,
            CommunityTemplateDownloadMaxPerHour = 2
        };

        Assert.Null(throttle.TryBeginDownloadAttempt(settings));
        throttle.RegisterSuccessfulDownload(settings);
        Assert.Null(throttle.TryBeginDownloadAttempt(settings));
        throttle.RegisterSuccessfulDownload(settings);

        var blocked = throttle.TryBeginDownloadAttempt(settings);
        Assert.NotNull(blocked);
        Assert.Equal(CommunityTemplateDownloadThrottleReason.HourlyDownloadQuota, blocked!.Value.ThrottleReason);
    }

    [Fact]
    public void MaxPerHourZero_DisablesHourlyCap()
    {
        var throttle = new CommunityTemplateDownloadThrottle();
        var settings = new AppSettings
        {
            CommunityTemplateDownloadMinIntervalSeconds = 0,
            CommunityTemplateDownloadMaxPerHour = 0
        };

        for (var i = 0; i < 5; i++)
        {
            Assert.Null(throttle.TryBeginDownloadAttempt(settings));
            throttle.RegisterSuccessfulDownload(settings);
        }
    }
}
