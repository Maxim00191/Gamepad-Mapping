using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using GamepadMapperGUI.Interfaces.Services;
using GamepadMapperGUI.Models.Core;

namespace GamepadMapperGUI.Services;

public class GitHubContentService : IGitHubContentService
{
    private const string MirrorProxyPrefix = "https://ghfast.top/";
    private readonly HttpClient _httpClient;
    public string BuildMirrorProxyUrl(string originalUrl, string? mirrorBaseUrl = null)
    {
        if (string.IsNullOrWhiteSpace(originalUrl))
            return string.Empty;
        var prefix = NormalizeMirrorPrefix(mirrorBaseUrl);
        if (originalUrl.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return originalUrl;
        return $"{prefix}{originalUrl}";
    }


    public GitHubContentService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "GamepadMapping-App");
    }

    public string BuildRawUrl(GitHubRepositoryContentRequest request)
    {
        var escaped = EncodePathSegments(request.RelativePath);
        return $"https://raw.githubusercontent.com/{request.Owner}/{request.Repository}/{request.Branch}/{escaped}";
    }

    public string BuildCdnUrl(GitHubRepositoryContentRequest request)
    {
        var escaped = EncodePathSegments(request.RelativePath);
        return $"https://fastly.jsdelivr.net/gh/{request.Owner}/{request.Repository}@{request.Branch}/{escaped}";
    }

    public async Task<GitHubRepositoryContentResult> GetTextWithPrimaryFallbackAsync(
        string primaryUrl,
        string fallbackUrl,
        bool preferFallback,
        TimeSpan primaryTimeout,
        CancellationToken cancellationToken = default)
    {
        if (preferFallback)
        {
            try
            {
                var content = await _httpClient.GetStringAsync(fallbackUrl, cancellationToken);
                return new GitHubRepositoryContentResult(content, UsedCdn: true);
            }
            catch
            {
                // Fallback endpoint failed; retry primary endpoint.
            }
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(primaryTimeout);
            var content = await _httpClient.GetStringAsync(primaryUrl, cts.Token);
            return new GitHubRepositoryContentResult(content, UsedCdn: false);
        }
        catch
        {
            var content = await _httpClient.GetStringAsync(fallbackUrl, cancellationToken);
            return new GitHubRepositoryContentResult(content, UsedCdn: true);
        }
    }

    public async Task<GitHubRepositoryContentResult> GetTextWithRawCdnFallbackAsync(
        GitHubRepositoryContentRequest request,
        bool preferCdn,
        TimeSpan rawTimeout,
        CancellationToken cancellationToken = default)
    {
        return await GetTextWithPrimaryFallbackAsync(
            BuildRawUrl(request),
            BuildCdnUrl(request),
            preferCdn,
            rawTimeout,
            cancellationToken);
    }

    public async Task<string> GetGitHubApiStringAsync(
        string apiUrl,
        string accept,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private static string EncodePathSegments(string relativePath)
    {
        var normalized = (relativePath ?? string.Empty).Replace('\\', '/');
        var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return string.Join('/', Array.ConvertAll(parts, Uri.EscapeDataString));
    }

    private static string NormalizeMirrorPrefix(string? mirrorBaseUrl)
    {
        var raw = string.IsNullOrWhiteSpace(mirrorBaseUrl) ? MirrorProxyPrefix : mirrorBaseUrl.Trim();
        return raw.EndsWith("/", StringComparison.Ordinal) ? raw : $"{raw}/";
    }
}
