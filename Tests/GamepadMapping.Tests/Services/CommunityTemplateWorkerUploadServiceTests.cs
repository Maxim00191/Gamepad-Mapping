using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Models.Core;
using GamepadMapperGUI.Models.Core.Community;
using GamepadMapperGUI.Services.Infrastructure;
using Moq;
using Xunit;

namespace GamepadMapping.Tests.Services;

public sealed class CommunityTemplateWorkerUploadServiceTests
{
    [Fact]
    public async Task SubmitBundleAsync_MissingWorkerUrl_ReturnsError()
    {
        var settings = new AppSettings { CommunityProfilesUploadWorkerUrl = "" };
        var compliance = new Mock<ICommunityTemplateUploadComplianceService>();
        var sut = new CommunityTemplateWorkerUploadService(settings, new HttpClient(), compliance.Object);

        var r = await sut.SubmitBundleAsync(
            [new GameProfileTemplate { ProfileId = "a" }],
            "Game",
            "Author",
            "Description text here.");

        Assert.False(r.Success);
        Assert.Contains("worker", r.ErrorMessage ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitBundleAsync_PostsJsonPayloadAndReturnsPrUrl()
    {
        Uri? capturedUri = null;
        HttpMethod? capturedMethod = null;
        string? capturedAuth = null;
        string? capturedCustomAuth = null;
        string? capturedBody = null;
        var handler = new DelegateHandler(async (req, ct) =>
        {
            capturedUri = req.RequestUri;
            capturedMethod = req.Method;
            capturedAuth = req.Headers.Authorization?.ToString();
            capturedCustomAuth = req.Headers.TryGetValues(CommunityUploadWorkerRequestHeaders.CustomAuthKey, out var vals)
                ? string.Join(",", vals)
                : null;
            capturedBody = req.Content is not null ? await req.Content.ReadAsStringAsync(ct) : null;
            var ack = """{"success":true,"pullRequestHtmlUrl":"https://example.com/pr/1"}""";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ack, Encoding.UTF8, "application/json")
            };
        });

        var settings = new AppSettings
        {
            CommunityProfilesUploadWorkerUrl = "https://upload.example/submit",
            CommunityProfilesRepoOwner = "o",
            CommunityProfilesRepoName = "r",
            CommunityProfilesRepoBranch = "main",
            CommunityProfilesUploadWorkerApiKey = "secret-key"
        };

        var compliance = new Mock<ICommunityTemplateUploadComplianceService>();
        compliance
            .Setup(c => c.EvaluateSubmission(
                It.IsAny<IReadOnlyList<CommunityTemplateBundleEntry>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns(new CommunityTemplateUploadComplianceResult(true, Array.Empty<CommunityTemplateComplianceStepResult>()));

        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://ignored/") };
        var sut = new CommunityTemplateWorkerUploadService(settings, http, compliance.Object);

        var r = await sut.SubmitBundleAsync(
            [new GameProfileTemplate { ProfileId = "my-profile" }],
            "MyGame",
            "Tester",
            "Listing here.");

        Assert.True(r.Success);
        Assert.Equal("https://example.com/pr/1", r.PullRequestHtmlUrl);
        Assert.NotNull(capturedUri);
        Assert.Equal(HttpMethod.Post, capturedMethod);
        Assert.Equal(new Uri("https://upload.example/submit"), capturedUri);
        Assert.Equal("Bearer secret-key", capturedAuth);
        Assert.Equal("secret-key", capturedCustomAuth);

        Assert.NotNull(capturedBody);
        Assert.Contains("\"schemaVersion\":1", capturedBody!, StringComparison.Ordinal);
        Assert.Contains("\"repoOwner\":\"o\"", capturedBody!, StringComparison.Ordinal);
        Assert.Contains("\"contentBase64\":", capturedBody!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SubmitBundleAsync_WorkerReturnsStructuredError_FormatsDiagnostics()
    {
        const string body =
            """{"success":false,"error":"Could not write template file","phase":"github_upload_file","detail":"File: g/a/x.json — Validation Failed","requestId":"rid-test","github":{"status":422,"message":"Validation Failed"}}""";
        var handler = new DelegateHandler((_, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        }));

        var settings = new AppSettings
        {
            CommunityProfilesUploadWorkerUrl = "https://upload.example/submit",
            CommunityProfilesRepoOwner = "o",
            CommunityProfilesRepoName = "r",
            CommunityProfilesRepoBranch = "main",
            CommunityProfilesUploadWorkerApiKey = "k"
        };

        var compliance = new Mock<ICommunityTemplateUploadComplianceService>();
        compliance
            .Setup(c => c.EvaluateSubmission(
                It.IsAny<IReadOnlyList<CommunityTemplateBundleEntry>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns(new CommunityTemplateUploadComplianceResult(true, Array.Empty<CommunityTemplateComplianceStepResult>()));

        using var http = new HttpClient(handler);
        var sut = new CommunityTemplateWorkerUploadService(settings, http, compliance.Object);

        var r = await sut.SubmitBundleAsync(
            [new GameProfileTemplate { ProfileId = "p1" }],
            "G",
            "A",
            "Description text here.");

        Assert.False(r.Success);
        Assert.NotNull(r.ErrorMessage);
        Assert.Contains("Could not write template file", r.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("File: g/a/x.json", r.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("Phase: github_upload_file", r.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("GitHub HTTP: 422", r.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("Request ID: rid-test", r.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("Worker HTTP: 502", r.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SubmitBundleAsync_LegacyWorkerMissingParameters_AppendsDeploymentHint()
    {
        const string body = """{"error":"Missing parameters"}""";
        var handler = new DelegateHandler((_, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        }));

        var settings = new AppSettings
        {
            CommunityProfilesUploadWorkerUrl = "https://upload.example/submit",
            CommunityProfilesRepoOwner = "o",
            CommunityProfilesRepoName = "r",
            CommunityProfilesRepoBranch = "main",
            CommunityProfilesUploadWorkerApiKey = "k"
        };

        var compliance = new Mock<ICommunityTemplateUploadComplianceService>();
        compliance
            .Setup(c => c.EvaluateSubmission(
                It.IsAny<IReadOnlyList<CommunityTemplateBundleEntry>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns(new CommunityTemplateUploadComplianceResult(true, Array.Empty<CommunityTemplateComplianceStepResult>()));

        using var http = new HttpClient(handler);
        var sut = new CommunityTemplateWorkerUploadService(settings, http, compliance.Object);

        var r = await sut.SubmitBundleAsync(
            [new GameProfileTemplate { ProfileId = "p1" }],
            "G",
            "A",
            "Description text here.");

        Assert.False(r.Success);
        Assert.NotNull(r.ErrorMessage);
        Assert.Contains("Missing parameters", r.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cloudflare/community-upload", r.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class DelegateHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _send;

        public DelegateHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        {
            _send = send;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => _send(request, cancellationToken);
    }
}
