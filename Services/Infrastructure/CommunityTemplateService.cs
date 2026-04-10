using System;
using System.Collections.Generic;
using System.Net.Http;
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
using GamepadMapperGUI.Utils;
using Newtonsoft.Json;

namespace GamepadMapperGUI.Services.Infrastructure;

public class CommunityTemplateService : ICommunityTemplateService
{
    private const string RepoOwner = "Maxim00191";
    private const string RepoName = "GamepadMapping-CommunityProfiles";
    private const string Branch = "main";

    private readonly IGitHubContentService _gitHubContentService;
    private readonly IProfileService _profileService;
    private readonly ILocalFileService _localFileService;

    private DateTime _lastRequestTime = DateTime.MinValue;
    private static readonly TimeSpan MinRequestInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan FallbackTimeout = TimeSpan.FromSeconds(4);

    // 状态标记：是否优先使用 CDN（一旦 GitHub 失败，本次会话后续请求将优先走 CDN）
    private bool _useCdnPreferred = false;

    public CommunityTemplateService(
        IProfileService profileService,
        IGitHubContentService? gitHubContentService = null,
        ILocalFileService? localFileService = null)
    {
        _profileService = profileService;
        _gitHubContentService = gitHubContentService ?? new GitHubContentService();
        _localFileService = localFileService ?? new LocalFileService();
    }

    public CommunityTemplateService(IProfileService profileService, HttpClient httpClient)
        : this(profileService, new GitHubContentService(httpClient), new LocalFileService())
    {
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
        => await DownloadTemplateAsync(template, allowOverwrite: true);

    public Task<CommunityTemplateDownloadPrecheckResult> CheckLocalTemplateConflictAsync(CommunityTemplateInfo template)
    {
        try
        {
            if (template is null || string.IsNullOrWhiteSpace(template.Id))
                return Task.FromResult(new CommunityTemplateDownloadPrecheckResult(false, null));

            var templatesRoot = _profileService.LoadTemplateDirectory();
            var candidatePath = AppPaths.TemplateCatalogPaths.GetTemplateJsonPath(
                templatesRoot,
                template.CatalogFolder,
                template.Id.Trim());

            if (!_localFileService.FileExists(candidatePath))
                return Task.FromResult(new CommunityTemplateDownloadPrecheckResult(false, null));

            var existingJson = _localFileService.ReadAllText(candidatePath);
            var existingTemplate = JsonConvert.DeserializeObject<GameProfileTemplate>(existingJson);
            if (existingTemplate is null)
                return Task.FromResult(new CommunityTemplateDownloadPrecheckResult(false, null));

            var sameId = string.Equals(
                (existingTemplate.ProfileId ?? string.Empty).Trim(),
                template.Id.Trim(),
                StringComparison.OrdinalIgnoreCase);
            var sameName = string.Equals(
                (existingTemplate.DisplayName ?? string.Empty).Trim(),
                (template.DisplayName ?? string.Empty).Trim(),
                StringComparison.OrdinalIgnoreCase);

            return Task.FromResult(new CommunityTemplateDownloadPrecheckResult(
                HasSameFolderIdAndName: sameId && sameName,
                ExistingDisplayName: existingTemplate.DisplayName));
        }
        catch (Exception ex)
        {
            App.Logger.Warning($"Local conflict precheck failed for template '{template?.Id}': {ex.Message}");
            return Task.FromResult(new CommunityTemplateDownloadPrecheckResult(false, null));
        }
    }

    public async Task<bool> DownloadTemplateAsync(CommunityTemplateInfo template, bool allowOverwrite = true)
    {
        try
        {
            App.Logger.Info($"Downloading template: {template.DisplayName} using URL: {template.DownloadUrl}");

            var request = CreateRequest(template.CatalogFolder, template.Id);
            var result = await _gitHubContentService.GetTextWithRawCdnFallbackAsync(
                request,
                preferCdn: _useCdnPreferred,
                rawTimeout: FallbackTimeout);
            _useCdnPreferred = result.UsedCdn;
            var json = result.Content;

            if (string.IsNullOrWhiteSpace(json))
                return false;

            var profileTemplate = JsonConvert.DeserializeObject<GameProfileTemplate>(json);
            if (profileTemplate == null) return false;

            profileTemplate.TemplateCatalogFolder = template.CatalogFolder;
            _profileService.SaveTemplate(profileTemplate, allowOverwrite);
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
        try
        {
            var request = new GitHubRepositoryContentRequest(RepoOwner, RepoName, Branch, relativePath);
            var result = await _gitHubContentService.GetTextWithRawCdnFallbackAsync(
                request,
                preferCdn: _useCdnPreferred,
                rawTimeout: FallbackTimeout);
            _useCdnPreferred = result.UsedCdn;
            return result.Content;
        }
        catch (Exception ex)
        {
            App.Logger.Error("Community fetch failed on both GitHub Raw and CDN.", ex);
            return null;
        }
    }

    private string GetEffectiveDownloadUrl(string? folder, string id)
    {
        return _useCdnPreferred ? GetCdnUrl(folder, id) : GetGitHubRawUrl(folder, id);
    }

    private static GitHubRepositoryContentRequest CreateRequest(string? folder, string id)
    {
        var relativePath = string.IsNullOrEmpty(folder) ? $"{id}.json" : $"{folder}/{id}.json";
        return new GitHubRepositoryContentRequest(RepoOwner, RepoName, Branch, relativePath);
    }

    private string GetGitHubRawUrl(string? folder, string id)
    {
        return _gitHubContentService.BuildRawUrl(CreateRequest(folder, id));
    }

    private string GetCdnUrl(string? folder, string id)
    {
        return _gitHubContentService.BuildCdnUrl(CreateRequest(folder, id));
    }
}


