using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Gamepad_Mapping;
using GamepadMapperGUI.Interfaces.Services;

namespace GamepadMapperGUI.Services;

public class UpdateService : IUpdateService
{
    private const string GitHubApiBase = "https://api.github.com/repos";
    private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 GamepadMapping/1.0";
    
    private readonly HttpClient _httpClient;

    public UpdateService()
    {
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

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));

            using var responseMessage = await _httpClient.SendAsync(request);
            if (responseMessage.IsSuccessStatusCode)
            {
                var response = await responseMessage.Content.ReadAsStringAsync();
                
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
            else
            {
                var responseContent = await responseMessage.Content.ReadAsStringAsync();
                isForbidden = responseMessage.StatusCode == System.Net.HttpStatusCode.Forbidden;
                errorMessage = isForbidden 
                    ? "GitHub API rate limit exceeded. Please check manually." 
                    : $"GitHub API error: {responseMessage.StatusCode}";
                
                App.Logger.Error($"[UpdateService] Check failed. Status: {responseMessage.StatusCode}, Response: {responseContent}");
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"Network error: {ex.Message}";
            App.Logger.Error($"[UpdateService] Exception: {ex.Message}", ex);
        }

        return new AppUpdateInfo(currentVersion, latestVersion, releaseUrl, isUpdateAvailable, errorMessage, isForbidden);
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

    private class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; set; }
    }
}
