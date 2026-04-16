using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GamepadMapperGUI.Models.Core;
using GamepadMapperGUI.Services.Infrastructure;
using GamepadMapperGUI.Services.Storage;
using GamepadMapperGUI.Services.Update;
using GamepadMapperGUI.Services.Input;
using GamepadMapperGUI.Services.Radial;
using Moq;
using Moq.Protected;

namespace GamepadMapping.Tests.Services;

public class GitHubContentServiceTests
{
    [Fact]
    public async Task GetTextWithPrimaryFallbackAsync_UsesFallbackWhenPrimaryFails()
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        const string primaryUrl = "https://primary.example.com/data.json";
        const string fallbackUrl = "https://fallback.example.com/data.json";

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString() == primaryUrl),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("primary down"));

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString() == fallbackUrl),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("fallback-content")
            });

        var service = new GitHubContentService(new HttpClient(handlerMock.Object));
        var result = await service.GetTextWithPrimaryFallbackAsync(
            primaryUrl,
            fallbackUrl,
            preferFallback: false,
            primaryTimeout: TimeSpan.FromSeconds(1));

        Assert.Equal("fallback-content", result.Content);
        Assert.True(result.UsedCdn);
    }

    [Fact]
    public void BuildMirrorProxyUrl_ReturnsStableMirrorUrl()
    {
        var service = new GitHubContentService();
        const string origin = "https://api.github.com/repos/foo/bar/releases/latest";
        const string mirrorPrefix = "https://ghfast.top/";

        var mirrored = service.BuildMirrorProxyUrl(origin, mirrorPrefix);
        var mirroredAgain = service.BuildMirrorProxyUrl(mirrored, mirrorPrefix);

        Assert.Equal("https://ghfast.top/https://api.github.com/repos/foo/bar/releases/latest", mirrored);
        Assert.Equal(mirrored, mirroredAgain);
    }

    [Fact]
    public async Task GetTextWithRawCdnFallbackAsync_DelegatesToCommonFallbackFlow()
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        var request = new GitHubRepositoryContentRequest("owner", "repo", "main", "index.json");

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.Host.Contains("raw.githubusercontent.com")),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("raw down"));

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.Host.Contains("fastly.jsdelivr.net")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("cdn-content")
            });

        var service = new GitHubContentService(new HttpClient(handlerMock.Object));
        var result = await service.GetTextWithRawCdnFallbackAsync(
            request,
            preferCdn: false,
            rawTimeout: TimeSpan.FromSeconds(1));

        Assert.Equal("cdn-content", result.Content);
        Assert.True(result.UsedCdn);
    }

    [Fact]
    public async Task GetTextWithRawCdnFallbackAsync_AppendsQuerySuffixToBothEndpoints()
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        var request = new GitHubRepositoryContentRequest("owner", "repo", "main", "index.json");
        const string expectedSuffix = "gm_cb=42";

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.RequestUri != null
                    && req.RequestUri.Host.Contains("raw.githubusercontent.com", StringComparison.Ordinal)
                    && req.RequestUri.Query.Contains("gm_cb=42", StringComparison.Ordinal)),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("fresh-index")
            });

        var service = new GitHubContentService(new HttpClient(handlerMock.Object));
        var result = await service.GetTextWithRawCdnFallbackAsync(
            request,
            preferCdn: false,
            rawTimeout: TimeSpan.FromSeconds(1),
            cancellationToken: default,
            requestQuerySuffix: expectedSuffix);

        Assert.Equal("fresh-index", result.Content);
        Assert.False(result.UsedCdn);
    }
}

