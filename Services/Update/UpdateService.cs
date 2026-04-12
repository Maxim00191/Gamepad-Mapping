using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Gamepad_Mapping;
using GamepadMapperGUI.Services.Infrastructure;
using GamepadMapperGUI.Services.Storage;
using GamepadMapperGUI.Services.Update;
using GamepadMapperGUI.Services.Input;
using GamepadMapperGUI.Services.Radial;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Storage;
using GamepadMapperGUI.Interfaces.Services.Update;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Interfaces.Services.Radial;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Models.Core;
using GamepadMapperGUI.Models.State;
using GamepadMapperGUI.Utils;

namespace GamepadMapperGUI.Services.Update;

public class UpdateService : IUpdateService
{
    private const string GitHubApiBase = "https://api.github.com/repos";
    private static readonly TimeSpan PrimaryEndpointTimeout = TimeSpan.FromSeconds(4);
    private const long MaxReleaseAssetBytes = 1024L * 1024L * 1024L;
    private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 GamepadMapping/1.0";
    
    private readonly HttpClient _httpClient;
    private readonly IGitHubContentService _gitHubContentService;
    private readonly IUpdateVersionCacheService _updateVersionCacheService;
    private readonly string _mirrorBaseUrl;
    private readonly string _signingPublicKeyPem;
    private bool _preferMirror;
    private string? _lastNetworkFallbackNotice;

    public UpdateService(
        IGitHubContentService? gitHubContentService = null,
        ISettingsService? settingsService = null,
        AppSettings? appSettings = null,
        IUpdateVersionCacheService? updateVersionCacheService = null)
    {
        _gitHubContentService = gitHubContentService ?? new GitHubContentService();
        _updateVersionCacheService = updateVersionCacheService ?? new UpdateVersionCacheService();
        var resolvedSettings = appSettings ?? (settingsService ?? new SettingsService()).LoadSettings();
        _mirrorBaseUrl = NormalizeMirrorBaseUrl(resolvedSettings.GithubMirrorBaseUrl);
        _signingPublicKeyPem = UpdateReleaseSigningPublicKey.Pem.Trim();
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
                    _updateVersionCacheService.SaveLatestVersion(owner, repo, latestVersion, releaseUrl);
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

            var candidateTag = (release.TagName ?? string.Empty).Trim();
            var currentVersion = GetCurrentVersion();
            if (!IsNewerVersion(currentVersion, candidateTag))
            {
                return new ReleaseResolutionResult(
                    release.TagName,
                    release.HtmlUrl,
                    null,
                    $"Rollback protection blocked install: release {candidateTag} is not newer than current version {currentVersion}.");
            }

            var highestTrustedTag = TryGetHighestTrustedReleaseTag();
            if (!string.IsNullOrWhiteSpace(highestTrustedTag) && IsOlderVersion(candidateTag, highestTrustedTag))
            {
                return new ReleaseResolutionResult(
                    release.TagName,
                    release.HtmlUrl,
                    null,
                    $"Rollback protection blocked install: release {candidateTag} is not newer than trusted baseline {highestTrustedTag}.");
            }

            if (release.Assets is null || release.Assets.Count == 0)
                return new ReleaseResolutionResult(release.TagName, release.HtmlUrl, null, "No downloadable assets in this release.");

            var matched = ResolveBestAsset(release.Assets, installMode);
            if (matched is null)
                return new ReleaseResolutionResult(release.TagName, release.HtmlUrl, null, $"No matching ZIP asset found for installation mode: {installMode}.");

            var expectedSha256 = await TryResolveSha256ForAssetAsync(release.Assets, matched, cancellationToken);
            if (string.IsNullOrWhiteSpace(expectedSha256))
            {
                return new ReleaseResolutionResult(
                    release.TagName,
                    release.HtmlUrl,
                    null,
                    "Signed checksum validation failed. Release packages must include verifiable SHA256SUMS.");
            }

            return new ReleaseResolutionResult(
                release.TagName,
                release.HtmlUrl,
                new ReleaseAssetInfo(matched.Name ?? "release.zip", matched.BrowserDownloadUrl ?? string.Empty, matched.Size, expectedSha256));
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
        if (!TryCreateSafeHttpsUri(assetDownloadUrl, out _))
            throw new InvalidOperationException("Blocked non-HTTPS asset URL. Falling back to official GitHub download is required.");

        var destinationDirectory = Path.GetDirectoryName(destinationFilePath);
        if (!string.IsNullOrWhiteSpace(destinationDirectory))
            Directory.CreateDirectory(destinationDirectory);

        using var response = await SendAssetRequestWithMirrorFallbackAsync(assetDownloadUrl, cancellationToken);

        var totalBytes = response.Content.Headers.ContentLength;
        if (totalBytes.HasValue && totalBytes.Value > MaxReleaseAssetBytes)
            throw new InvalidOperationException($"Blocked oversized update package ({totalBytes.Value} bytes). Maximum allowed is {MaxReleaseAssetBytes} bytes.");
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
            if (bytesReceived > MaxReleaseAssetBytes)
                throw new InvalidOperationException($"Blocked oversized update package stream (>{MaxReleaseAssetBytes} bytes).");

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

    public string? ConsumeLastNetworkFallbackNotice()
    {
        return Interlocked.Exchange(ref _lastNetworkFallbackNotice, null);
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

        // Remove build metadata while preserving pre-release labels (alpha/beta/rc).
        if (version.Contains('+', StringComparison.Ordinal))
            version = version.Split('+')[0];

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

    private bool IsOlderVersion(string candidate, string baseline) =>
        !IsNewerVersion(candidate, baseline) && !AreSameVersion(candidate, baseline);

    private static bool AreSameVersion(string a, string b) =>
        string.Equals(NormalizeVersionForEquality(a), NormalizeVersionForEquality(b), StringComparison.OrdinalIgnoreCase);

    private static string NormalizeVersionForEquality(string version)
    {
        var v = (version ?? string.Empty).Trim().TrimStart('v');
        var plus = v.IndexOf('+');
        if (plus >= 0)
            v = v[..plus];
        return v;
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
        if (!TryCreateSafeHttpsUri(apiUrl, out _))
            throw new InvalidOperationException("Blocked non-HTTPS GitHub API URL.");

        var mirrorEnabled = !string.IsNullOrWhiteSpace(_mirrorBaseUrl);
        var mirrorUrl = mirrorEnabled ? _gitHubContentService.BuildMirrorProxyUrl(apiUrl, _mirrorBaseUrl) : string.Empty;
        var mirrorAllowed = TryCreateSafeHttpsUri(mirrorUrl, out _);
        if (mirrorEnabled && !mirrorAllowed)
            SetNetworkFallbackNotice();
        if (_preferMirror && mirrorEnabled)
        {
            try
            {
                if (mirrorAllowed)
                {
                    var mirrored = await _gitHubContentService.GetGitHubApiStringAsync(
                        mirrorUrl,
                        accept,
                        cancellationToken);
                    return mirrored;
                }
            }
            catch
            {
                // Mirror unavailable, fallback to GitHub origin.
                SetNetworkFallbackNotice();
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
            if (!mirrorEnabled || !mirrorAllowed)
                throw;
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
        if (!TryCreateSafeHttpsUri(originUrl, out _))
            throw new InvalidOperationException("Blocked non-HTTPS asset URL.");

        var mirrorEnabled = !string.IsNullOrWhiteSpace(_mirrorBaseUrl);
        var mirrorUrl = mirrorEnabled ? _gitHubContentService.BuildMirrorProxyUrl(originUrl, _mirrorBaseUrl) : string.Empty;
        var mirrorAllowed = TryCreateSafeHttpsUri(mirrorUrl, out _);
        if (mirrorEnabled && !mirrorAllowed)
            SetNetworkFallbackNotice();

        if (_preferMirror && mirrorEnabled)
        {
            try
            {
                if (mirrorAllowed)
                {
                    var mirroredResponse = await SendAssetRequestAsync(mirrorUrl, cancellationToken);
                    _preferMirror = true;
                    return mirroredResponse;
                }
            }
            catch
            {
                // Mirror failed; fallback to origin.
                SetNetworkFallbackNotice();
            }
        }

        try
        {
            var originResponse = await SendAssetRequestAsync(originUrl, cancellationToken);
            _preferMirror = false;
            return originResponse;
        }
        catch
        {
            if (!mirrorEnabled || !mirrorAllowed)
                throw;
            var mirroredResponse = await SendAssetRequestAsync(mirrorUrl, cancellationToken);
            _preferMirror = true;
            return mirroredResponse;
        }
    }

    private async Task<HttpResponseMessage> SendAssetRequestAsync(
        string url,
        CancellationToken cancellationToken)
    {
        if (!TryCreateSafeHttpsUri(url, out _))
            throw new InvalidOperationException($"Blocked non-HTTPS asset endpoint: {url}");

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        return response;
    }

    private static string NormalizeMirrorBaseUrl(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;
        var trimmed = raw.Trim();
        if (!TryCreateSafeHttpsUri(trimmed, out _))
            return string.Empty;
        return trimmed.EndsWith("/", StringComparison.Ordinal) ? trimmed : $"{trimmed}/";
    }

    private static bool TryCreateSafeHttpsUri(string? raw, out Uri? uri)
    {
        uri = null;
        if (string.IsNullOrWhiteSpace(raw))
            return false;
        if (!Uri.TryCreate(raw.Trim(), UriKind.Absolute, out var parsed))
            return false;
        if (!string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return false;
        if (string.IsNullOrWhiteSpace(parsed.Host))
            return false;
        uri = parsed;
        return true;
    }

    private void SetNetworkFallbackNotice()
    {
        const string notice = "检测到镜像地址异常或不可用，请检查你的URL，正在回退使用GitHub官方链接。";
        Interlocked.Exchange(ref _lastNetworkFallbackNotice, notice);
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
            _ => null
        };

        if (string.IsNullOrWhiteSpace(preferredToken))
            return null;

        // Whitelist strategy: only accept explicitly tagged package variants.
        return zipAssets.FirstOrDefault(x => ContainsToken(x, preferredToken));
    }

    private async Task<string?> TryResolveSha256ForAssetAsync(
        IReadOnlyList<GitHubAsset> assets,
        GitHubAsset packageAsset,
        CancellationToken cancellationToken)
    {
        var packageName = packageAsset.Name ?? string.Empty;
        if (packageName.Length == 0)
            return null;

        var checksumsAsset = assets.FirstOrDefault(a =>
            string.Equals(a.Name, "SHA256SUMS", StringComparison.OrdinalIgnoreCase));
        var signatureAsset = assets.FirstOrDefault(a =>
            string.Equals(a.Name, "SHA256SUMS.sig", StringComparison.OrdinalIgnoreCase));
        if (checksumsAsset?.BrowserDownloadUrl is null || signatureAsset?.BrowserDownloadUrl is null)
            return null;

        try
        {
            var checksumBytes = await DownloadBinaryAssetWithMirrorFallbackAsync(checksumsAsset.BrowserDownloadUrl, cancellationToken);
            var signatureBytes = await DownloadBinaryAssetWithMirrorFallbackAsync(signatureAsset.BrowserDownloadUrl, cancellationToken);
            if (!VerifyChecksumSignature(checksumBytes, signatureBytes))
                return null;
            PersistVerifiedChecksumArtifacts(checksumBytes, signatureBytes);
            var checksumContent = Encoding.UTF8.GetString(checksumBytes);
            return ParseSha256FromChecksumContent(checksumContent, packageName);
        }
        catch
        {
            return null;
        }
    }

    private async Task<byte[]> DownloadBinaryAssetWithMirrorFallbackAsync(string assetDownloadUrl, CancellationToken cancellationToken)
    {
        using var response = await SendAssetRequestWithMirrorFallbackAsync(assetDownloadUrl, cancellationToken);
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    private bool VerifyChecksumSignature(byte[] checksumBytes, byte[] signatureBytes)
    {
        if (checksumBytes is null || checksumBytes.Length == 0 || signatureBytes is null || signatureBytes.Length == 0)
            return false;
        if (string.IsNullOrWhiteSpace(_signingPublicKeyPem))
            return false;

        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(_signingPublicKeyPem);
            return rsa.VerifyData(checksumBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch
        {
            return false;
        }
    }

    private static void PersistVerifiedChecksumArtifacts(byte[] checksumBytes, byte[] signatureBytes)
    {
        try
        {
            var updatesDir = AppPaths.GetUpdateDownloadsDirectory();
            var checksumPath = Path.Combine(updatesDir, "SHA256SUMS");
            var signaturePath = Path.Combine(updatesDir, "SHA256SUMS.sig");
            File.WriteAllBytes(checksumPath, checksumBytes);
            File.WriteAllBytes(signaturePath, signatureBytes);
        }
        catch
        {
            // Best effort cache for local reinstall path; live download/install flow should continue.
        }
    }

    private string? TryGetHighestTrustedReleaseTag()
    {
        try
        {
            var path = AppPaths.GetUpdateSecurityStateFilePath();
            if (!File.Exists(path))
                return null;

            var json = File.ReadAllText(path, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            var state = JsonSerializer.Deserialize<UpdateSecurityState>(json);
            return state?.HighestTrustedReleaseTag?.Trim();
        }
        catch
        {
            return null;
        }
    }

    private static string? ParseSha256FromChecksumContent(string content, string packageFileName)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        var lines = content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var escapedName = Regex.Escape(packageFileName);
        var pattern = $@"\b([A-Fa-f0-9]{{64}})\b(?:\s+\*?{escapedName})?$";
        foreach (var line in lines)
        {
            var match = Regex.Match(line, pattern, RegexOptions.CultureInvariant);
            if (match.Success)
                return match.Groups[1].Value.ToLowerInvariant();
        }

        if (lines.Length == 1)
        {
            var single = Regex.Match(lines[0], @"\b([A-Fa-f0-9]{64})\b", RegexOptions.CultureInvariant);
            if (single.Success)
                return single.Groups[1].Value.ToLowerInvariant();
        }

        return null;
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


