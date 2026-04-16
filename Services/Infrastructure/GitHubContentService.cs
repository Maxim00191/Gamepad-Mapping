using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Storage;
using GamepadMapperGUI.Interfaces.Services.Update;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Interfaces.Services.Radial;
using GamepadMapperGUI.Models.Core;

namespace GamepadMapperGUI.Services.Infrastructure;

public class GitHubContentService : IGitHubContentService
{
    private readonly HttpClient _httpClient;
    public string BuildMirrorProxyUrl(string originalUrl, string? mirrorBaseUrl = null)
    {
        if (string.IsNullOrWhiteSpace(originalUrl))
            return string.Empty;
        var prefix = NormalizeMirrorPrefix(mirrorBaseUrl);
        if (string.IsNullOrWhiteSpace(prefix))
            return originalUrl;
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
        CancellationToken cancellationToken = default,
        string? requestQuerySuffix = null)
    {
        var primary = BuildRawUrl(request);
        var fallback = BuildCdnUrl(request);
        if (!string.IsNullOrWhiteSpace(requestQuerySuffix))
        {
            var suffix = requestQuerySuffix.Trim();
            if (suffix.StartsWith('?'))
                suffix = suffix[1..];
            primary = AppendQuery(primary, suffix);
            fallback = AppendQuery(fallback, suffix);
        }

        return await GetTextWithPrimaryFallbackAsync(
            primary,
            fallback,
            preferCdn,
            rawTimeout,
            cancellationToken);
    }

    private static string AppendQuery(string url, string nameAndValue)
    {
        var sep = url.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return string.Concat(url, sep, nameAndValue);
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
        var raw = string.IsNullOrWhiteSpace(mirrorBaseUrl) ? string.Empty : mirrorBaseUrl.Trim();
        if (raw.Length == 0)
            return string.Empty;
        return raw.EndsWith("/", StringComparison.Ordinal) ? raw : $"{raw}/";
    }
}


