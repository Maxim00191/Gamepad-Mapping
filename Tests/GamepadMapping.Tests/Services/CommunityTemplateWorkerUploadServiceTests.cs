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
        string? capturedSigVersion = null;
        string? capturedSigTimestamp = null;
        string? capturedSigNonce = null;
        string? capturedSigContentHash = null;
        string? capturedSig = null;
        string? capturedTicketId = null;
        string? capturedTicketProof = null;
        string? capturedBody = null;
        var handler = new DelegateHandler(async (req, ct) =>
        {
            if (IsTicketRequest(req))
                return BuildTicketSuccessResponse();

            capturedUri = req.RequestUri;
            capturedMethod = req.Method;
            capturedAuth = req.Headers.Authorization?.ToString();
            capturedCustomAuth = req.Headers.TryGetValues(CommunityUploadWorkerRequestHeaders.CustomAuthKey, out var vals)
                ? string.Join(",", vals)
                : null;
            capturedSigVersion = req.Headers.TryGetValues(CommunityUploadWorkerRequestHeaders.SignatureVersionKey, out var sigVersionVals)
                ? string.Join(",", sigVersionVals)
                : null;
            capturedSigTimestamp = req.Headers.TryGetValues(CommunityUploadWorkerRequestHeaders.TimestampSecondsKey, out var sigTimestampVals)
                ? string.Join(",", sigTimestampVals)
                : null;
            capturedSigNonce = req.Headers.TryGetValues(CommunityUploadWorkerRequestHeaders.NonceKey, out var sigNonceVals)
                ? string.Join(",", sigNonceVals)
                : null;
            capturedSigContentHash = req.Headers.TryGetValues(CommunityUploadWorkerRequestHeaders.ContentSha256Key, out var sigHashVals)
                ? string.Join(",", sigHashVals)
                : null;
            capturedSig = req.Headers.TryGetValues(CommunityUploadWorkerRequestHeaders.SignatureKey, out var sigVals)
                ? string.Join(",", sigVals)
                : null;
            capturedTicketId = req.Headers.TryGetValues(CommunityUploadWorkerRequestHeaders.TicketIdKey, out var ticketIdVals)
                ? string.Join(",", ticketIdVals)
                : null;
            capturedTicketProof = req.Headers.TryGetValues(CommunityUploadWorkerRequestHeaders.TicketProofKey, out var ticketProofVals)
                ? string.Join(",", ticketProofVals)
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
        var fixedSigner = new FixedSigner();
        var sut = new CommunityTemplateWorkerUploadService(settings, http, compliance.Object, fixedSigner);

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
        Assert.Equal(CommunityUploadWorkerRequestHeaders.SignatureVersionV1, capturedSigVersion);
        Assert.Equal("1700000000", capturedSigTimestamp);
        Assert.Equal("nonce-fixed", capturedSigNonce);
        Assert.Equal("hash-fixed", capturedSigContentHash);
        Assert.Equal("sig-fixed", capturedSig);
        Assert.Equal("ticket-1", capturedTicketId);
        Assert.Equal("proof-1", capturedTicketProof);

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
        var handler = new DelegateHandler((req, ct) =>
        {
            if (IsTicketRequest(req))
                return Task.FromResult(BuildTicketSuccessResponse());

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        });

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
        var handler = new DelegateHandler((req, ct) =>
        {
            if (IsTicketRequest(req))
                return Task.FromResult(BuildTicketSuccessResponse());

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        });

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

    [Fact]
    public async Task SubmitBundleAsync_PipelineBusyError_ReturnsBusyFlag()
    {
        const string body =
            """{"success":false,"error":"Pipeline busy","code":"pipeline_busy","phase":"workflow_in_progress","requestId":"rid-ci"}""";
        var handler = new DelegateHandler((req, _) =>
        {
            if (IsTicketRequest(req))
                return Task.FromResult(BuildTicketSuccessResponse());

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        });

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
        Assert.True(r.IsPipelineBusy);
        Assert.Contains("Pipeline busy", r.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitBundleAsync_MoreThanMaxFiles_ReturnsValidationError()
    {
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

        var templates = new List<GameProfileTemplate>();
        for (var i = 0; i < CommunityTemplateUploadConstraints.MaxFilesPerSubmission + 1; i++)
            templates.Add(new GameProfileTemplate { ProfileId = $"p{i}" });

        var sut = new CommunityTemplateWorkerUploadService(settings, new HttpClient(), compliance.Object);

        var r = await sut.SubmitBundleAsync(
            templates,
            "G",
            "A",
            "Description text here.");

        Assert.False(r.Success);
        Assert.Contains("up to", r.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            CommunityTemplateUploadConstraints.MaxFilesPerSubmission.ToString(),
            r.ErrorMessage ?? string.Empty,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task SubmitBundleAsync_TemplateLargerThanLimit_ReturnsValidationError()
    {
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

        var oversizedTemplate = new GameProfileTemplate
        {
            ProfileId = "p1",
            DisplayName = new string('X', CommunityTemplateUploadConstraints.MaxTemplateFileBytes + 2048)
        };

        var sut = new CommunityTemplateWorkerUploadService(settings, new HttpClient(), compliance.Object);

        var r = await sut.SubmitBundleAsync(
            [oversizedTemplate],
            "G",
            "A",
            "Description text here.");

        Assert.False(r.Success);
        Assert.Contains("exceeds", r.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            CommunityTemplateUploadConstraints.MaxTemplateFileBytes.ToString(),
            r.ErrorMessage ?? string.Empty,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task SubmitBundleAsync_LockedStatusWithoutBody_ReturnsBusyFlag()
    {
        var handler = new DelegateHandler((req, _) =>
        {
            if (IsTicketRequest(req))
                return Task.FromResult(BuildTicketSuccessResponse());

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Locked)
            {
                Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
            });
        });

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
        Assert.True(r.IsPipelineBusy);
    }

    [Fact]
    public async Task SubmitBundleAsync_UnparsedBusyMarker_ReturnsBusyFlag()
    {
        const string body = "workflow in progress";
        var handler = new DelegateHandler((req, _) =>
        {
            if (IsTicketRequest(req))
                return Task.FromResult(BuildTicketSuccessResponse());

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway)
            {
                Content = new StringContent(body, Encoding.UTF8, "text/plain")
            });
        });

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
        Assert.True(r.IsPipelineBusy);
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

    private static bool IsTicketRequest(HttpRequestMessage request)
    {
        var path = request.RequestUri?.AbsolutePath ?? string.Empty;
        return path.EndsWith("/ticket", StringComparison.OrdinalIgnoreCase);
    }

    private static HttpResponseMessage BuildTicketSuccessResponse()
    {
        const string ticketAck = """{"success":true,"ticketId":"ticket-1","ticketProof":"proof-1","expiresAtUnixSeconds":1999999999}""";
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ticketAck, Encoding.UTF8, "application/json")
        };
    }

    private sealed class FixedSigner : ICommunityUploadWorkerRequestSigner
    {
        public void ApplySignatureHeaders(HttpRequestMessage request, Uri endpointUri, string requestBody, string signingKey)
        {
            request.Headers.TryAddWithoutValidation(CommunityUploadWorkerRequestHeaders.SignatureVersionKey, CommunityUploadWorkerRequestHeaders.SignatureVersionV1);
            request.Headers.TryAddWithoutValidation(CommunityUploadWorkerRequestHeaders.TimestampSecondsKey, "1700000000");
            request.Headers.TryAddWithoutValidation(CommunityUploadWorkerRequestHeaders.NonceKey, "nonce-fixed");
            request.Headers.TryAddWithoutValidation(CommunityUploadWorkerRequestHeaders.ContentSha256Key, "hash-fixed");
            request.Headers.TryAddWithoutValidation(CommunityUploadWorkerRequestHeaders.SignatureKey, "sig-fixed");
        }
    }
}
