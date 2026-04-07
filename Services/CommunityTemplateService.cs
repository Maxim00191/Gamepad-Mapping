using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Gamepad_Mapping;
using GamepadMapperGUI.Interfaces.Services;
using GamepadMapperGUI.Models;
using Newtonsoft.Json;

namespace GamepadMapperGUI.Services;

public class CommunityTemplateService : ICommunityTemplateService
{
    private readonly HttpClient _httpClient;
    private readonly IProfileService _profileService;
    
    // 生产环境指向 main 分支
    private const string GitHubRawBase = "https://raw.githubusercontent.com/Maxim00191/GamepadMapping-CommunityProfiles/main";
    private const string CdnBase = "https://fastly.jsdelivr.net/gh/Maxim00191/GamepadMapping-CommunityProfiles@main";
    
    private DateTime _lastRequestTime = DateTime.MinValue;
    private static readonly TimeSpan MinRequestInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan FallbackTimeout = TimeSpan.FromSeconds(4);

    // 状态标记：是否优先使用 CDN（一旦 GitHub 失败，本次会话后续请求将优先走 CDN）
    private bool _useCdnPreferred = false;

    public CommunityTemplateService(IProfileService profileService, HttpClient? httpClient = null)
    {
        _profileService = profileService;
        _httpClient = httpClient ?? new HttpClient();
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "GamepadMapping-App");
        }
    }

    public async Task<List<CommunityTemplateInfo>> GetTemplatesAsync()
    {
        if (DateTime.Now - _lastRequestTime < MinRequestInterval)
        {
            App.Logger.Warning("Request throttled: Refreshing too fast.");
            return new List<CommunityTemplateInfo>();
        }
        _lastRequestTime = DateTime.Now;

        try
        {
            App.Logger.Info("Fetching community index with fallback strategy...");
            var json = await DownloadStringWithFallbackAsync("index.json");
            
            if (string.IsNullOrEmpty(json))
            {
                App.Logger.Error("Failed to fetch community index from both GitHub and CDN.");
                return new List<CommunityTemplateInfo>();
            }

            var templates = JsonConvert.DeserializeObject<List<CommunityTemplateInfo>>(json) ?? new List<CommunityTemplateInfo>();
            
            // 预处理下载链接，确保下载时也遵循降级逻辑
            foreach (var t in templates)
            {
                t.DownloadUrl = GetEffectiveDownloadUrl(t.CatalogFolder, t.Id);
            }

            App.Logger.Info($"Successfully loaded {templates.Count} templates.");
            return templates;
        }
        catch (Exception ex)
        {
            App.Logger.Error("Unexpected error while fetching community index", ex);
            return new List<CommunityTemplateInfo>();
        }
    }

    public async Task<bool> DownloadTemplateAsync(CommunityTemplateInfo template)
    {
        try
        {
            App.Logger.Info($"Downloading template: {template.DisplayName} using URL: {template.DownloadUrl}");
            
            string? json = null;
            try
            {
                json = await _httpClient.GetStringAsync(template.DownloadUrl);
            }
            catch
            {
                App.Logger.Warning($"Primary download failed for {template.DisplayName}, attempting fallback...");
                var fallbackUrl = template.DownloadUrl.Contains("raw.githubusercontent.com") 
                    ? GetCdnUrl(template.CatalogFolder, template.Id)
                    : GetGitHubRawUrl(template.CatalogFolder, template.Id);
                
                json = await _httpClient.GetStringAsync(fallbackUrl);
            }

            var profileTemplate = JsonConvert.DeserializeObject<GameProfileTemplate>(json);
            if (profileTemplate == null) return false;

            profileTemplate.TemplateCatalogFolder = template.CatalogFolder;
            _profileService.SaveTemplate(profileTemplate, allowOverwrite: true);
            _profileService.ReloadTemplates(profileTemplate.ProfileId);
            
            return true;
        }
        catch (Exception ex)
        {
            App.Logger.Error($"Failed to download template {template.DisplayName}", ex);
            return false;
        }
    }

    private async Task<string?> DownloadStringWithFallbackAsync(string relativePath)
    {
        // 1. 如果已知 GitHub 不通，直接尝试 CDN
        if (_useCdnPreferred)
        {
            try { return await _httpClient.GetStringAsync($"{CdnBase}/{relativePath}"); }
            catch { /* 继续尝试 GitHub 以防网络恢复 */ }
        }

        // 2. 尝试从 GitHub 下载
        try
        {
            using var cts = new CancellationTokenSource(FallbackTimeout);
            var url = $"{GitHubRawBase}/{relativePath}";
            App.Logger.Debug($"Attempting GitHub: {url}");
            return await _httpClient.GetStringAsync(url, cts.Token);
        }
        catch (Exception ex)
        {
            App.Logger.Warning($"GitHub access failed or timed out, falling back to CDN: {ex.Message}");
            _useCdnPreferred = true;
            
            // 3. 降级到 CDN
            try
            {
                var url = $"{CdnBase}/{relativePath}";
                App.Logger.Debug($"Attempting CDN: {url}");
                return await _httpClient.GetStringAsync(url);
            }
            catch (Exception cdnEx)
            {
                App.Logger.Error("CDN access also failed.", cdnEx);
                return null;
            }
        }
    }

    private string GetEffectiveDownloadUrl(string? folder, string id)
    {
        return _useCdnPreferred ? GetCdnUrl(folder, id) : GetGitHubRawUrl(folder, id);
    }

    private string GetGitHubRawUrl(string? folder, string id)
    {
        var path = string.IsNullOrEmpty(folder) ? $"{id}.json" : $"{folder}/{id}.json";
        return $"{GitHubRawBase}/{Uri.EscapeDataString(path)}";
    }

    private string GetCdnUrl(string? folder, string id)
    {
        var path = string.IsNullOrEmpty(folder) ? $"{id}.json" : $"{folder}/{id}.json";
        return $"{CdnBase}/{Uri.EscapeDataString(path)}";
    }
}
