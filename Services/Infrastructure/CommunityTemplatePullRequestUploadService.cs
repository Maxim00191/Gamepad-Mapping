using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Models.Core;
using GamepadMapperGUI.Models.Core.Community;
using Newtonsoft.Json;

namespace GamepadMapperGUI.Services.Infrastructure;

public sealed class CommunityTemplatePullRequestUploadService : ICommunityTemplateUploadService
{
    private const string GitHubApiVersion = "2022-11-28";
    private static readonly Uri ApiRoot = new("https://api.github.com/", UriKind.Absolute);

    private readonly AppSettings _settings;
    private readonly HttpClient _httpClient;
    private readonly ICommunityTemplateUploadComplianceService _compliance;

    public CommunityTemplatePullRequestUploadService(
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
        var token = (_settings.CommunityProfilesUploadToken ?? string.Empty).Trim();
        if (token.Length == 0)
        {
            return new CommunityTemplateUploadResult(
                false,
                null,
                "Missing upload credentials. Set communityProfilesUploadWorkerUrl (recommended) or communityProfilesUploadToken in local_settings.json (PAT needs repo scope).");
        }

        if (templates is null || templates.Count == 0)
            return new CommunityTemplateUploadResult(false, null, "No templates to upload.");

        var desc = (communityListingDescription ?? string.Empty).Trim();
        if (desc.Length == 0)
            return new CommunityTemplateUploadResult(false, null, "Listing description is required.");

        var authorTrimmedEarly = (authorDisplayName ?? string.Empty).Trim();
        if (!CommunityTemplateUploadMetadataValidator.IsMetadataValidForSubmission(authorTrimmedEarly, desc, out var metadataError))
            return new CommunityTemplateUploadResult(false, null, metadataError);

        var owner = (_settings.CommunityProfilesRepoOwner ?? string.Empty).Trim();
        var repo = (_settings.CommunityProfilesRepoName ?? string.Empty).Trim();
        var baseBranch = (_settings.CommunityProfilesRepoBranch ?? string.Empty).Trim();
        if (owner.Length == 0 || repo.Length == 0 || baseBranch.Length == 0)
            return new CommunityTemplateUploadResult(false, null, "Community repository settings are incomplete.");

        var authorTrimmed = authorTrimmedEarly;

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

        try
        {
            var baseSha = await GetBranchHeadShaAsync(token, owner, repo, baseBranch, cancellationToken)
                .ConfigureAwait(false);
            if (baseSha is null)
                return new CommunityTemplateUploadResult(false, null, $"Could not resolve branch '{baseBranch}'.");

            var branchName = $"community/upload-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}";
            if (branchName.Length > 120)
                branchName = branchName[..120];

            await CreateBranchAsync(token, owner, repo, branchName, baseSha, cancellationToken)
                .ConfigureAwait(false);

            foreach (var t in templates)
            {
                var clone = CommunityTemplateSubmissionClone.CloneForSubmission(t, catalogFolder, authorTrimmed, desc);
                var relativePath = $"{catalogFolder.Replace('\\', '/')}/{clone.ProfileId.Trim()}.json";
                var json = JsonConvert.SerializeObject(clone, Newtonsoft.Json.Formatting.Indented);
                await PutRepositoryFileAsync(token, owner, repo, relativePath, branchName, json, cancellationToken)
                    .ConfigureAwait(false);
            }

            var prUrl = await CreatePullRequestAsync(
                    token,
                    owner,
                    repo,
                    branchName,
                    baseBranch,
                    $"Community templates: {gameSeg} ({authorSeg})",
                    BuildPrBody(catalogFolder, templates.Select(x => x.ProfileId).ToList(), desc),
                    cancellationToken)
                .ConfigureAwait(false);

            return new CommunityTemplateUploadResult(true, prUrl, null);
        }
        catch (HttpRequestException ex)
        {
            return new CommunityTemplateUploadResult(false, null, ex.Message);
        }
    }

    private static string BuildPrBody(string catalogFolder, IReadOnlyList<string> profileIds, string listingDescription)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Automated community template submission from Gamepad Mapping.");
        sb.AppendLine();
        sb.AppendLine($"**Catalog folder:** `{catalogFolder}`");
        sb.AppendLine();
        sb.AppendLine("**Templates:**");
        foreach (var id in profileIds)
            sb.AppendLine($"- `{id}`");
        sb.AppendLine();
        sb.AppendLine("**Listing description:**");
        sb.AppendLine(listingDescription);
        sb.AppendLine();
        sb.AppendLine("After merge, the index workflow should refresh `index.json`.");
        return sb.ToString();
    }

    private async Task<string?> GetBranchHeadShaAsync(
        string token,
        string owner,
        string repo,
        string branch,
        CancellationToken cancellationToken)
    {
        var rel =
            $"repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repo)}/git/ref/heads/{Uri.EscapeDataString(branch)}";
        using var request = CreateGitHubRequest(HttpMethod.Get, rel, token);
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return null;

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("object").GetProperty("sha").GetString();
    }

    private async Task CreateBranchAsync(
        string token,
        string owner,
        string repo,
        string newBranch,
        string baseSha,
        CancellationToken cancellationToken)
    {
        var url = $"repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repo)}/git/refs";
        var payload = new Dictionary<string, string>
        {
            ["ref"] = $"refs/heads/{newBranch}",
            ["sha"] = baseSha
        };
        using var request = CreateGitHubRequest(HttpMethod.Post, url, token);
        request.Content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Create branch failed: {(int)response.StatusCode} {body}");
    }

    private async Task PutRepositoryFileAsync(
        string token,
        string owner,
        string repo,
        string relativePath,
        string branch,
        string json,
        CancellationToken cancellationToken)
    {
        var escaped = EscapeGithubContentPath(relativePath);
        var url = $"repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repo)}/contents/{escaped}";
        var existingSha = await TryGetBlobShaAsync(token, owner, repo, escaped, branch, cancellationToken)
            .ConfigureAwait(false);

        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        var payload = new Dictionary<string, object?>
        {
            ["message"] = $"feat(community): add {relativePath}",
            ["content"] = base64,
            ["branch"] = branch
        };
        if (existingSha is not null)
            payload["sha"] = existingSha;

        using var request = CreateGitHubRequest(HttpMethod.Put, url, token);
        request.Content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Upload file failed: {(int)response.StatusCode} {body}");
    }

    private async Task<string?> TryGetBlobShaAsync(
        string token,
        string owner,
        string repo,
        string escapedPath,
        string branch,
        CancellationToken cancellationToken)
    {
        var url =
            $"repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repo)}/contents/{escapedPath}?ref={Uri.EscapeDataString(branch)}";
        using var request = CreateGitHubRequest(HttpMethod.Get, url, token);
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return null;
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("sha").GetString();
    }

    private async Task<string?> CreatePullRequestAsync(
        string token,
        string owner,
        string repo,
        string headBranch,
        string baseBranch,
        string title,
        string body,
        CancellationToken cancellationToken)
    {
        var url = $"repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repo)}/pulls";
        var payload = new Dictionary<string, string>
        {
            ["title"] = title,
            ["body"] = body,
            ["head"] = headBranch,
            ["base"] = baseBranch
        };
        using var request = CreateGitHubRequest(HttpMethod.Post, url, token);
        request.Content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Create pull request failed: {(int)response.StatusCode} {responseBody}");

        using var doc = JsonDocument.Parse(responseBody);
        return doc.RootElement.GetProperty("html_url").GetString();
    }

    private HttpRequestMessage CreateGitHubRequest(HttpMethod method, string relativeUrl, string token)
    {
        if (!relativeUrl.StartsWith("repos/", StringComparison.Ordinal))
            throw new ArgumentException("Expected a relative GitHub API path.", nameof(relativeUrl));

        var request = new HttpRequestMessage(method, new Uri(ApiRoot, relativeUrl));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", GitHubApiVersion);
        request.Headers.UserAgent.ParseAdd("GamepadMapping-CommunityUpload/1.0");
        return request;
    }

    private static string EscapeGithubContentPath(string relativePath)
    {
        var n = relativePath.Replace('\\', '/').Trim('/');
        var parts = n.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join("/", parts.Select(Uri.EscapeDataString));
    }
}
