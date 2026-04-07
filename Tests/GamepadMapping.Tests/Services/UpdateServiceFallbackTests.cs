using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GamepadMapperGUI.Interfaces.Services;
using GamepadMapperGUI.Services;
using Moq;

namespace GamepadMapping.Tests.Services;

public class UpdateServiceFallbackTests
{
    [Fact]
    public async Task CheckForUpdatesAsync_FallsBackToMirrorWhenOriginFails()
    {
        var contentMock = new Mock<IGitHubContentService>(MockBehavior.Strict);
        var originUrl = "https://api.github.com/repos/owner/repo/releases/latest";
        var mirrorUrl = $"https://ghfast.top/{originUrl}";
        var releaseJson = "{\"tag_name\":\"v9.9.9\",\"html_url\":\"https://github.com/owner/repo/releases/tag/v9.9.9\"}";

        contentMock.Setup(x => x.BuildMirrorProxyUrl(originUrl, It.IsAny<string?>())).Returns(mirrorUrl);
        contentMock.Setup(x => x.GetGitHubApiStringAsync(
                originUrl,
                "application/vnd.github.v3+json",
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("origin blocked"));
        contentMock.Setup(x => x.GetGitHubApiStringAsync(
                mirrorUrl,
                "application/vnd.github.v3+json",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(releaseJson);

        var service = new UpdateService(contentMock.Object);
        var result = await service.CheckForUpdatesAsync("owner", "repo", includePrereleases: false);

        Assert.True(result.IsUpdateAvailable);
        Assert.Equal("v9.9.9", result.LatestVersion);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_PrefersMirrorAfterFirstSuccessfulFallback()
    {
        var contentMock = new Mock<IGitHubContentService>(MockBehavior.Strict);
        var originUrl = "https://api.github.com/repos/owner/repo/releases/latest";
        var mirrorUrl = $"https://ghfast.top/{originUrl}";
        var releaseJson = "{\"tag_name\":\"v9.9.9\",\"html_url\":\"https://github.com/owner/repo/releases/tag/v9.9.9\"}";

        contentMock.Setup(x => x.BuildMirrorProxyUrl(originUrl, It.IsAny<string?>())).Returns(mirrorUrl);
        contentMock.Setup(x => x.GetGitHubApiStringAsync(
                originUrl,
                "application/vnd.github.v3+json",
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("origin blocked"));
        contentMock.Setup(x => x.GetGitHubApiStringAsync(
                mirrorUrl,
                "application/vnd.github.v3+json",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(releaseJson);

        var service = new UpdateService(contentMock.Object);
        _ = await service.CheckForUpdatesAsync("owner", "repo", includePrereleases: false);
        _ = await service.CheckForUpdatesAsync("owner", "repo", includePrereleases: false);

        contentMock.Verify(x => x.GetGitHubApiStringAsync(
            originUrl,
            "application/vnd.github.v3+json",
            It.IsAny<CancellationToken>()), Times.Once);
        contentMock.Verify(x => x.GetGitHubApiStringAsync(
            mirrorUrl,
            "application/vnd.github.v3+json",
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
