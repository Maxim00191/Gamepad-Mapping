using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using StjJsonSerializer = System.Text.Json.JsonSerializer;
using StjJsonException = System.Text.Json.JsonException;
using System.Threading;
using System.Threading.Tasks;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Models.Core;
using GamepadMapperGUI.Models.Core.Community;
using Newtonsoft.Json;

namespace GamepadMapperGUI.Services.Infrastructure;

public sealed class CommunityTemplateWorkerUploadService : ICommunityTemplateUploadService
{
    private static readonly string[] PipelineBusyMarkers =
    [
        "pipeline_busy",
        "pipeline busy",
        "ci_busy",
        "ci busy",
        "workflow_busy",
        "workflow busy",
        "workflow_in_progress",
        "workflow in progress"
    ];

    private const string LegacyMissingParametersDeploymentHint =
        "The upload URL appears to be running an older worker that only accepts {\"fileName\",\"content\"}. "
        + "This app sends the community bundle format (schemaVersion, files with relativePath and contentBase64). "
        + "Deploy the Worker from the GamepadMapping-CommunityProfiles repo (cloudflare/community-upload) "
        + "using wrangler, or point communityProfilesUploadWorkerUrl at that deployment.";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonSerializerOptions DeserializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly AppSettings _settings;
    private readonly HttpClient _httpClient;
    private readonly ICommunityTemplateUploadComplianceService _compliance;

    public CommunityTemplateWorkerUploadService(
        AppSettings settings,
        HttpClient? httpClient = null,
        ICommunityTemplateUploadComplianceService? complianceService = null)
    {
        _settings = settings;
        _httpClient = httpClient ?? new HttpClient();
        _compliance = complianceService ?? new CommunityTemplateUploadComplianceService();
    }

    public async Task<CommunityTemplateUploadResult> SubmitBundleAsync(
        IReadOnlyList<GameProfileTemplate> templates,
        string gameFolderDisplayName,
        string authorDisplayName,
        string communityListingDescription,
        CancellationToken cancellationToken = default)
    {
        var endpoint = (_settings.CommunityProfilesUploadWorkerUrl ?? string.Empty).Trim();
        if (endpoint.Length == 0)
        {
            return new CommunityTemplateUploadResult(
                false,
                null,
                "Missing upload worker URL. Set communityProfilesUploadWorkerUrl in local_settings.json.");
        }

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri)
            || endpointUri.Scheme != Uri.UriSchemeHttps && endpointUri.Scheme != Uri.UriSchemeHttp)
        {
            return new CommunityTemplateUploadResult(
                false,
                null,
                "communityProfilesUploadWorkerUrl must be an absolute http(s) URL.");
        }

        var uploadWorkerApiKey = CommunityUploadWorkerCredentials.ResolveUploadWorkerApiKey(_settings.CommunityProfilesUploadWorkerApiKey);
        if (uploadWorkerApiKey.Length == 0)
        {
            return new CommunityTemplateUploadResult(
                false,
                null,
                "Community upload API key is not set. Add communityProfilesUploadWorkerApiKey to Assets/Config/local_settings.json with the same value as the Cloudflare Worker secret UPLOAD_API_KEY (or build with the embedded key).");
        }

        if (templates is null || templates.Count == 0)
            return new CommunityTemplateUploadResult(false, null, "No templates to upload.");
        if (templates.Count > CommunityTemplateUploadConstraints.MaxFilesPerSubmission)
        {
            return new CommunityTemplateUploadResult(
                false,
                null,
                string.Format(
                    CultureInfo.CurrentCulture,
                    "Upload accepts up to {0} template files per request.",
                    CommunityTemplateUploadConstraints.MaxFilesPerSubmission));
        }

        var desc = (communityListingDescription ?? string.Empty).Trim();
        if (desc.Length == 0)
            return new CommunityTemplateUploadResult(false, null, "Listing description is required.");

        var owner = (_settings.CommunityProfilesRepoOwner ?? string.Empty).Trim();
        var repo = (_settings.CommunityProfilesRepoName ?? string.Empty).Trim();
        var baseBranch = (_settings.CommunityProfilesRepoBranch ?? string.Empty).Trim();
        if (owner.Length == 0 || repo.Length == 0 || baseBranch.Length == 0)
            return new CommunityTemplateUploadResult(false, null, "Community repository settings are incomplete.");

        var authorTrimmed = (authorDisplayName ?? string.Empty).Trim();

        string gameSeg;
        string authorSeg;
        try
        {
            gameSeg = TemplateStorageKey.ValidateSingleSegmentFolderForSave(gameFolderDisplayName);
            authorSeg = TemplateStorageKey.ValidateSingleSegmentFolderForSave(authorTrimmed);
        }
        catch (ArgumentException ex)
        {
            return new CommunityTemplateUploadResult(false, null, ex.Message);
        }

        if (gameSeg.Length == 0 || authorSeg.Length == 0)
            return new CommunityTemplateUploadResult(false, null, "Game folder and author name are required.");

        var catalogFolder = TemplateStorageKey.ValidateCatalogFolderPathForSave($"{gameSeg}/{authorSeg}");

        var bundleEntries = templates
            .Select((t, i) => new CommunityTemplateBundleEntry($"{i}:{(t.ProfileId ?? string.Empty).Trim()}", t))
            .ToList();
        var complianceResult = _compliance.EvaluateSubmission(
            bundleEntries,
            gameFolderDisplayName,
            authorTrimmed,
            desc);
        if (!complianceResult.ReadyToSubmit)
        {
            var lines = new List<string>();
            foreach (var step in complianceResult.Steps)
            {
                if (step.Severity != CommunityTemplateComplianceSeverity.Error || step.Issues.Count == 0)
                    continue;

                foreach (var issue in step.Issues)
                {
                    var line = string.IsNullOrEmpty(issue.TemplateLabel)
                        ? issue.Detail
                        : $"{issue.TemplateLabel}: {issue.Detail}";
                    lines.Add(line);
                    if (lines.Count >= 12)
                        break;
                }

                if (lines.Count >= 12)
                    break;
            }

            var msg = lines.Count > 0
                ? string.Join(Environment.NewLine, lines)
                : "Template validation failed.";
            return new CommunityTemplateUploadResult(false, null, msg);
        }

        var files = new List<CommunityTemplateWorkerSubmissionFile>();
        foreach (var t in templates)
        {
            var clone = CommunityTemplateSubmissionClone.CloneForSubmission(t, catalogFolder, authorTrimmed, desc);
            var relativePath = $"{catalogFolder.Replace('\\', '/')}/{clone.ProfileId.Trim()}.json";
            if (!relativePath.EndsWith(CommunityTemplateUploadConstraints.RequiredFileExtension, StringComparison.OrdinalIgnoreCase))
            {
                return new CommunityTemplateUploadResult(
                    false,
                    null,
                    string.Format(
                        CultureInfo.CurrentCulture,
                        "Only {0} files can be uploaded.",
                        CommunityTemplateUploadConstraints.RequiredFileExtension));
            }
            var json = JsonConvert.SerializeObject(clone, Newtonsoft.Json.Formatting.Indented);
            var jsonBytes = Encoding.UTF8.GetByteCount(json);
            if (jsonBytes > CommunityTemplateUploadConstraints.MaxTemplateFileBytes)
            {
                return new CommunityTemplateUploadResult(
                    false,
                    null,
                    string.Format(
                        CultureInfo.CurrentCulture,
                        "Template file exceeds {0} bytes: {1}",
                        CommunityTemplateUploadConstraints.MaxTemplateFileBytes,
                        relativePath));
            }
            files.Add(new CommunityTemplateWorkerSubmissionFile
            {
                RelativePath = relativePath,
                ContentBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json))
            });
        }

        var payload = new CommunityTemplateWorkerSubmissionPayload
        {
            SchemaVersion = CommunityTemplateWorkerSubmissionPayload.CurrentSchemaVersion,
            RepoOwner = owner,
            RepoName = repo,
            BaseBranch = baseBranch,
            CatalogFolder = catalogFolder.Replace('\\', '/'),
            GameFolderSegment = gameSeg,
            AuthorSegment = authorSeg,
            ListingDescription = desc,
            ProfileIds = templates.Select(x => (x.ProfileId ?? string.Empty).Trim()).ToList(),
            Files = files
        };

        try
        {
            var body = StjJsonSerializer.Serialize(payload, SerializerOptions);
            using var request = new HttpRequestMessage(HttpMethod.Post, endpointUri)
            {
                Content = new StringContent(body, Encoding.UTF8, new MediaTypeHeaderValue("application/json"))
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.UserAgent.ParseAdd("GamepadMapping-CommunityUpload/1.0");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", uploadWorkerApiKey);
            request.Headers.TryAddWithoutValidation(
                CommunityUploadWorkerRequestHeaders.CustomAuthKey,
                uploadWorkerApiKey);

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var httpCode = (int)response.StatusCode;

            CommunityTemplateWorkerSubmissionAck? ack = null;
            try
            {
                ack = StjJsonSerializer.Deserialize<CommunityTemplateWorkerSubmissionAck>(responseText, DeserializerOptions);
            }
            catch (StjJsonException)
            {
            }

            if (ack?.Success == true)
            {
                if (!string.IsNullOrWhiteSpace(ack.PullRequestHtmlUrl))
                    return new CommunityTemplateUploadResult(true, ack.PullRequestHtmlUrl, null);

                var missingUrlMsg = string.IsNullOrWhiteSpace(ack.RequestId)
                    ? "Upload reported success but no pull request URL was returned."
                    : string.Format(
                        CultureInfo.CurrentCulture,
                        "Upload reported success but no pull request URL was returned. Request ID: {0}",
                        ack.RequestId.Trim());
                return new CommunityTemplateUploadResult(false, null, missingUrlMsg);
            }

            if (ack is not null)
            {
                var failureText = ack.BuildFailureMessage(httpCode);
                if (httpCode == 401)
                {
                    failureText += Environment.NewLine
                        + "Confirm communityProfilesUploadWorkerApiKey in local_settings.json matches the Worker’s UPLOAD_API_KEY secret (no extra spaces; property name spelled exactly).";
                }

                failureText = AppendLegacyWorkerHintIfApplicable(httpCode, responseText, failureText);
                return new CommunityTemplateUploadResult(
                    false,
                    null,
                    failureText,
                    IsPipelineBusy(httpCode, ack, responseText));
            }

            var unparsed = CommunityTemplateWorkerSubmissionAck.BuildUnparsedFailureMessage(httpCode, responseText);
            unparsed = AppendLegacyWorkerHintIfApplicable(httpCode, responseText, unparsed);
            return new CommunityTemplateUploadResult(
                false,
                null,
                unparsed,
                IsPipelineBusy(httpCode, null, responseText));
        }
        catch (HttpRequestException ex)
        {
            return new CommunityTemplateUploadResult(false, null, ex.Message);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            return new CommunityTemplateUploadResult(false, null, ex.Message);
        }
    }

    private static string AppendLegacyWorkerHintIfApplicable(int httpStatusCode, string? responseBody, string message)
    {
        if (httpStatusCode != 400 || !LooksLikeLegacyMissingParametersWorker(responseBody))
            return message;

        return message.TrimEnd() + Environment.NewLine + Environment.NewLine + LegacyMissingParametersDeploymentHint;
    }

    private static bool LooksLikeLegacyMissingParametersWorker(string? responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            if (root.TryGetProperty("requestId", out _))
                return false;

            if (!root.TryGetProperty("error", out var errEl))
                return false;

            var err = errEl.GetString();
            return string.Equals(err, "Missing parameters", StringComparison.OrdinalIgnoreCase);
        }
        catch (StjJsonException)
        {
            return false;
        }
    }

    private static bool IsPipelineBusy(
        int workerHttpStatusCode,
        CommunityTemplateWorkerSubmissionAck? ack,
        string? responseBody)
    {
        if (workerHttpStatusCode is 409 or 423)
            return true;

        if (ack is not null)
        {
            if (ContainsPipelineBusyMarker(ack.Code)
                || ContainsPipelineBusyMarker(ack.Phase)
                || ContainsPipelineBusyMarker(ack.Error)
                || ContainsPipelineBusyMarker(ack.Detail))
            {
                return true;
            }
        }

        return ContainsPipelineBusyMarker(responseBody);
    }

    private static bool ContainsPipelineBusyMarker(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        foreach (var marker in PipelineBusyMarkers)
        {
            if (text.Contains(marker, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
