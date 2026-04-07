using System;
using System.Threading;
using System.Threading.Tasks;
using GamepadMapperGUI.Models.Core;

namespace GamepadMapperGUI.Interfaces.Services;

public interface IGitHubContentService
{
    string BuildMirrorProxyUrl(string originalUrl, string? mirrorBaseUrl = null);
    string BuildRawUrl(GitHubRepositoryContentRequest request);
    string BuildCdnUrl(GitHubRepositoryContentRequest request);
    Task<GitHubRepositoryContentResult> GetTextWithPrimaryFallbackAsync(
        string primaryUrl,
        string fallbackUrl,
        bool preferFallback,
        TimeSpan primaryTimeout,
        CancellationToken cancellationToken = default);
    Task<GitHubRepositoryContentResult> GetTextWithRawCdnFallbackAsync(
        GitHubRepositoryContentRequest request,
        bool preferCdn,
        TimeSpan rawTimeout,
        CancellationToken cancellationToken = default);
    Task<string> GetGitHubApiStringAsync(
        string apiUrl,
        string accept,
        CancellationToken cancellationToken = default);
}
