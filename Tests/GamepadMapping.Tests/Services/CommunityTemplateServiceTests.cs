using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Storage;
using GamepadMapperGUI.Interfaces.Services.Update;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Interfaces.Services.Radial;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Services.Infrastructure;
using GamepadMapperGUI.Services.Storage;
using GamepadMapperGUI.Services.Update;
using GamepadMapperGUI.Services.Input;
using GamepadMapperGUI.Services.Radial;
using Moq;
using Moq.Protected;
using Xunit;

namespace GamepadMapping.Tests.Services;

public class CommunityTemplateServiceTests
{
    private readonly Mock<IProfileService> _mockProfileService;

    public CommunityTemplateServiceTests()
    {
        _mockProfileService = new Mock<IProfileService>();
    }

    [Fact]
    public async Task GetTemplatesAsync_FallbackToCdn_WhenGitHubFails()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        
        // 模拟 GitHub 失败（抛出异常或返回错误）
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri != null && req.RequestUri.ToString().Contains("raw.githubusercontent.com")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(new HttpRequestException("GitHub is down"));

        // 模拟 CDN 成功
        var indexJson = "[{\"id\": \"test\", \"displayName\": \"Test Template\", \"author\": \"Tester\", \"catalogFolder\": \"Test\"}]";
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri != null && req.RequestUri.ToString().Contains("fastly.jsdelivr.net")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(indexJson)
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var service = new CommunityTemplateService(_mockProfileService.Object, httpClient);

        // Act
        var result = await service.GetTemplatesAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal("Test Template", result[0].DisplayName);
        Assert.Contains("fastly.jsdelivr.net", result[0].DownloadUrl); // 验证下载链接也降级到了 CDN
    }

    [Fact]
    public async Task GetTemplatesAsync_UsesGitHub_WhenAvailable()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        var indexJson = "[{\"id\": \"test\", \"displayName\": \"GitHub Template\", \"author\": \"Tester\", \"catalogFolder\": \"Test\"}]";

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri != null && req.RequestUri.ToString().Contains("raw.githubusercontent.com")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(indexJson)
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var service = new CommunityTemplateService(_mockProfileService.Object, httpClient);

        // Act
        var result = await service.GetTemplatesAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal("GitHub Template", result[0].DisplayName);
        Assert.Contains("raw.githubusercontent.com", result[0].DownloadUrl);
    }

    [Fact]
    public async Task GetTemplatesAsync_PreservesExplicitFileNameInNestedCatalogPath()
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        var indexJson =
            "[{\"id\":\"profile-id\",\"displayName\":\"Nested\",\"author\":\"Tester\",\"catalogFolder\":\"My Game/Alice\",\"fileName\":\"published-name.json\"}]";

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri != null && req.RequestUri.ToString().Contains("raw.githubusercontent.com")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(indexJson)
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var service = new CommunityTemplateService(_mockProfileService.Object, httpClient);

        var result = await service.GetTemplatesAsync();

        Assert.Single(result);
        Assert.Equal("My Game/Alice/published-name.json", result[0].RelativePath);
        Assert.EndsWith("/My%20Game/Alice/published-name.json", result[0].DownloadUrl, StringComparison.Ordinal);
    }
}


