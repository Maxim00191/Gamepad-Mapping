using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Gamepad_Mapping;
using GamepadMapperGUI.Interfaces.Services;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Models.Core;

namespace GamepadMapperGUI.Services;

public class UpdateService : IUpdateService
{
    private const string GitHubApiBase = "https://api.github.com/repos";
    private static readonly TimeSpan PrimaryEndpointTimeout = TimeSpan.FromSeconds(4);
    private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 GamepadMapping/1.0";
    
    private readonly HttpClient _httpClient;
    private readonly IGitHubContentService _gitHubContentService;
    private readonly string _mirrorBaseUrl;
    private bool _preferMirror;

    public UpdateService(
        IGitHubContentService? gitHubContentService = null,
        ISettingsService? settingsService = null,
        AppSettings? appSettings = null)
    {
        _gitHubContentService = gitHubContentService ?? new GitHubContentService();
        var resolvedSettings = appSettings ?? (settingsService ?? new SettingsService()).LoadSettings();
        _mirrorBaseUrl = NormalizeMirrorBaseUrl(resolvedSettings.GithubMirrorBaseUrl);
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
    }

    public async Task<AppUpdateInfo> CheckForUpdatesAsync(string owner, string repo, bool includePrereleases)
    {
        string currentVersion = GetCurrentVersion();
        string? latestVersion = null;
        string? releaseUrl = null;
        bool isUpdateAvailable = false;
        string? errorMessage = null;
        bool isForbidden = false;

        try
        {
            string url = includePrereleases 
                ? $"{GitHubApiBase}/{owner}/{repo}/releases" 
                : $"{GitHubApiBase}/{owner}/{repo}/releases/latest";

            App.Logger.Info($"[UpdateService] Checking for updates (IncludePrereleases: {includePrereleases}). URL: {url}");

            try
            {
                var response = await GetGitHubApiStringWithMirrorFallbackAsync(
                    url,
                    "application/vnd.github.v3+json");
                
                GitHubRelease? latestRelease = null;
                if (includePrereleases)
                {
                    var releases = JsonSerializer.Deserialize<List<GitHubRelease>>(response);
                    latestRelease = releases?.FirstOrDefault();
                }
                else
                {
                    latestRelease = JsonSerializer.Deserialize<GitHubRelease>(response);
                }

                if (latestRelease?.TagName != null)
                {
                    latestVersion = latestRelease.TagName;
                    isUpdateAvailable = IsNewerVersion(currentVersion, latestVersion);
                    releaseUrl = latestRelease.HtmlUrl;
                    App.Logger.Info($"[UpdateService] Update check successful. Latest: {latestVersion}, Current: {currentVersion}");
                }
                else
                {
                    errorMessage = "No release information found.";
                    App.Logger.Warning($"[UpdateService] {errorMessage}");
                }
            }
            catch (HttpRequestException httpEx)
            {
                isForbidden = httpEx.StatusCode == System.Net.HttpStatusCode.Forbidden;
                errorMessage = isForbidden 
                    ? "GitHub API rate limit exceeded. Please check manually." 
                    : $"GitHub API error: {httpEx.StatusCode}";
                
                App.Logger.Error($"[UpdateService] Check failed. Status: {httpEx.StatusCode}, Message: {httpEx.Message}");
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"Network error: {ex.Message}";
            App.Logger.Error($"[UpdateService] Exception: {ex.Message}", ex);
        }

        return new AppUpdateInfo(currentVersion, latestVersion, releaseUrl, isUpdateAvailable, errorMessage, isForbidden);
    }

    public async Task<ReleaseResolutionResult> ResolveReleaseAssetAsync(string owner, string repo, bool includePrereleases, AppInstallMode installMode, CancellationToken cancellationToken = default)
    {
        try
        {
            var release = await GetLatestReleaseAsync(owner, repo, includePrereleases, cancellationToken);
            if (release is null)
                return new ReleaseResolutionResult(null, null, null, "No release information found.");

            if (release.Assets is null || release.Assets.Count == 0)
                return new ReleaseResolutionResult(release.TagName, release.HtmlUrl, null, "No downloadable assets in this release.");

            var matched = ResolveBestAsset(release.Assets, installMode);
            if (matched is null)
                return new ReleaseResolutionResult(release.TagName, release.HtmlUrl, null, $"No matching ZIP asset found for installation mode: {installMode}.");

            return new ReleaseResolutionResult(
                release.TagName,
                release.HtmlUrl,
                new ReleaseAssetInfo(matched.Name ?? "release.zip", matched.BrowserDownloadUrl ?? string.Empty, matched.Size));
        }
        catch (Exception ex)
        {
            App.Logger.Error($"[UpdateService] Resolve release asset failed: {ex.Message}", ex);
            return new ReleaseResolutionResult(null, null, null, $"Failed to resolve release asset: {ex.Message}");
        }
    }

    public async Task DownloadReleaseAssetAsync(string assetDownloadUrl, string destinationFilePath, IProgress<ReleaseDownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(assetDownloadUrl))
            throw new ArgumentException("Asset download URL is required.", nameof(assetDownloadUrl));

        var destinationDirectory = Path.GetDirectoryName(destinationFilePath);
        if (!string.IsNullOrWhiteSpace(destinationDirectory))
            Directory.CreateDirectory(destinationDirectory);

        using var response = await SendAssetRequestWithMirrorFallbackAsync(assetDownloadUrl, cancellationToken);

        var totalBytes = response.Content.Headers.ContentLength;
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = new FileStream(destinationFilePath, FileMode.Create, FileAccess.Write, FileShare.None);

        var buffer = new byte[1024 * 64];
        long bytesReceived = 0;
        var stopwatch = Stopwatch.StartNew();
        var lastReportAt = TimeSpan.Zero;

        while (true)
        {
            var read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read <= 0)
                break;

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            bytesReceived += read;

            var elapsed = stopwatch.Elapsed;
            if (elapsed - lastReportAt < TimeSpan.FromMilliseconds(200) && totalBytes.HasValue && bytesReceived < totalBytes.Value)
                continue;

            var speed = elapsed.TotalSeconds > 0 ? bytesReceived / elapsed.TotalSeconds : 0d;
            TimeSpan? eta = null;
            if (totalBytes.HasValue && speed > 1d)
            {
                var remainingBytes = Math.Max(0, totalBytes.Value - bytesReceived);
                eta = TimeSpan.FromSeconds(remainingBytes / speed);
            }

            progress?.Report(new ReleaseDownloadProgress(bytesReceived, totalBytes, speed, eta));
            lastReportAt = elapsed;
        }

        progress?.Report(new ReleaseDownloadProgress(bytesReceived, totalBytes, 0d, TimeSpan.Zero));
    }

    private string GetCurrentVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = CustomAttributeExtensions.GetCustomAttribute<AssemblyInformationalVersionAttribute>(assembly)?.InformationalVersion;
        
        if (string.IsNullOrEmpty(version))
        {
            var v = assembly.GetName().Version;
            version = v != null ? $"{v.Major}.{v.Minor}.{v.Build}" : "1.0.0";
        }
        return version;
    }

    private bool IsNewerVersion(string current, string latest)
    {
        if (string.IsNullOrEmpty(latest)) return false;
        
        var c = current.TrimStart('v');
        var l = latest.TrimStart('v');

        if (Version.TryParse(c, out var vCurrent) && Version.TryParse(l, out var vLatest))
        {
            return vLatest > vCurrent;
        }

        var cParts = c.Split('-', 2);
        var lParts = l.Split('-', 2);

        if (Version.TryParse(cParts[0], out var vcBase) && Version.TryParse(lParts[0], out var vlBase))
        {
            if (vlBase > vcBase) return true;
            if (vlBase < vcBase) return false;

            // Base versions are equal, compare labels (stable > beta)
            if (cParts.Length > 1 && lParts.Length == 1) return true; 
            if (cParts.Length == 1 && lParts.Length > 1) return false; 
            
            if (cParts.Length > 1 && lParts.Length > 1)
            {
                return string.Compare(lParts[1], cParts[1], StringComparison.OrdinalIgnoreCase) > 0;
            }
        }
        
        return string.Compare(l, c, StringComparison.OrdinalIgnoreCase) > 0;
    }

    private async Task<GitHubRelease?> GetLatestReleaseAsync(string owner, string repo, bool includePrereleases, CancellationToken cancellationToken)
    {
        var url = includePrereleases
            ? $"{GitHubApiBase}/{owner}/{repo}/releases"
            : $"{GitHubApiBase}/{owner}/{repo}/releases/latest";

        var json = await GetGitHubApiStringWithMirrorFallbackAsync(
            url,
            "application/vnd.github.v3+json",
            cancellationToken);
        if (includePrereleases)
        {
            var releases = JsonSerializer.Deserialize<List<GitHubRelease>>(json);
            return releases?.FirstOrDefault();
        }

        return JsonSerializer.Deserialize<GitHubRelease>(json);
    }

    private async Task<string> GetGitHubApiStringWithMirrorFallbackAsync(
        string apiUrl,
        string accept,
        CancellationToken cancellationToken = default)
    {
        var mirrorUrl = _gitHubContentService.BuildMirrorProxyUrl(apiUrl, _mirrorBaseUrl);
        if (_preferMirror)
        {
            try
            {
                var mirrored = await _gitHubContentService.GetGitHubApiStringAsync(
                    mirrorUrl,
                    accept,
                    cancellationToken);
                return mirrored;
            }
            catch
            {
                // Mirror unavailable, fallback to GitHub origin.
            }
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(PrimaryEndpointTimeout);
            var primary = await _gitHubContentService.GetGitHubApiStringAsync(
                apiUrl,
                accept,
                cts.Token);
            _preferMirror = false;
            return primary;
        }
        catch
        {
            var mirrored = await _gitHubContentService.GetGitHubApiStringAsync(
                mirrorUrl,
                accept,
                cancellationToken);
            _preferMirror = true;
            return mirrored;
        }
    }

    private async Task<HttpResponseMessage> SendAssetRequestWithMirrorFallbackAsync(
        string originUrl,
        CancellationToken cancellationToken)
    {
        var mirrorUrl = _gitHubContentService.BuildMirrorProxyUrl(originUrl, _mirrorBaseUrl);

        if (_preferMirror)
        {
            try
            {
                var mirroredResponse = await SendAssetRequestAsync(mirrorUrl, cancellationToken);
                _preferMirror = true;
                return mirroredResponse;
            }
            catch
            {
                // Mirror failed; fallback to origin.
            }
        }

        try
        {
            var originResponse = await SendAssetRequestAsync(originUrl, cancellationToken, applyPrimaryTimeout: true);
            _preferMirror = false;
            return originResponse;
        }
        catch
        {
            var mirroredResponse = await SendAssetRequestAsync(mirrorUrl, cancellationToken);
            _preferMirror = true;
            return mirroredResponse;
        }
    }

    private async Task<HttpResponseMessage> SendAssetRequestAsync(
        string url,
        CancellationToken cancellationToken,
        bool applyPrimaryTimeout = false)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));

        if (!applyPrimaryTimeout)
        {
            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            return response;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(PrimaryEndpointTimeout);
        var timeoutResponse = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        timeoutResponse.EnsureSuccessStatusCode();
        return timeoutResponse;
    }

    private static string NormalizeMirrorBaseUrl(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "https://ghfast.top/";
        var trimmed = raw.Trim();
        return trimmed.EndsWith("/", StringComparison.Ordinal) ? trimmed : $"{trimmed}/";
    }

    private static GitHubAsset? ResolveBestAsset(IReadOnlyList<GitHubAsset> assets, AppInstallMode installMode)
    {
        static bool IsZip(GitHubAsset a) => (a.Name ?? string.Empty).EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
        static bool ContainsToken(GitHubAsset a, string token) => (a.Name ?? string.Empty).Contains(token, StringComparison.OrdinalIgnoreCase);

        var zipAssets = assets.Where(IsZip).ToList();
        if (zipAssets.Count == 0)
            return null;

        var preferredToken = installMode switch
        {
            AppInstallMode.Fx => "-fx",
            AppInstallMode.Single => "-single",
            _ => string.Empty
        };

        if (!string.IsNullOrEmpty(preferredToken))
        {
            var preferred = zipAssets.FirstOrDefault(x => ContainsToken(x, preferredToken));
            if (preferred is not null)
                return preferred;
        }

        // Fallback strategy for future packaging variants: first zip from release assets.
        return zipAssets.FirstOrDefault();
    }

    private class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; set; }

        [JsonPropertyName("assets")]
        public List<GitHubAsset> Assets { get; set; } = new();
    }

    private class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }

        [JsonPropertyName("size")]
        public long? Size { get; set; }
    }
}
