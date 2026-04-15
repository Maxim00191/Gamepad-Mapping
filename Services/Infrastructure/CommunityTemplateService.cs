using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
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
using GamepadMapperGUI.Utils;
using Newtonsoft.Json;

namespace GamepadMapperGUI.Services.Infrastructure;

public class CommunityTemplateService : ICommunityTemplateService
{
    private readonly IGitHubContentService _gitHubContentService;
    private readonly IProfileService _profileService;
    private readonly ILocalFileService _localFileService;
    private readonly AppSettings _communityRepoSettings;

    private DateTime _lastRequestTime = DateTime.MinValue;
    private static readonly TimeSpan MinRequestInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan FallbackTimeout = TimeSpan.FromSeconds(4);

    // 状态标记：是否优先使用 CDN（一旦 GitHub 失败，本次会话后续请求将优先走 CDN）
    private bool _useCdnPreferred = false;

    public CommunityTemplateService(
        IProfileService profileService,
        IGitHubContentService? gitHubContentService = null,
        ILocalFileService? localFileService = null,
        AppSettings? communityRepoSettings = null)
    {
        _profileService = profileService;
        _gitHubContentService = gitHubContentService ?? new GitHubContentService();
        _localFileService = localFileService ?? new LocalFileService();
        _communityRepoSettings = communityRepoSettings ?? new AppSettings();
    }

    public CommunityTemplateService(IProfileService profileService, HttpClient httpClient)
        : this(profileService, new GitHubContentService(httpClient), new LocalFileService(), null)
    {
    }

    private string RepoOwner =>
        string.IsNullOrWhiteSpace(_communityRepoSettings.CommunityProfilesRepoOwner)
            ? "Maxim00191"
            : _communityRepoSettings.CommunityProfilesRepoOwner.Trim();

    private string RepoName =>
        string.IsNullOrWhiteSpace(_communityRepoSettings.CommunityProfilesRepoName)
            ? "GamepadMapping-CommunityProfiles"
            : _communityRepoSettings.CommunityProfilesRepoName.Trim();

    private string Branch =>
        string.IsNullOrWhiteSpace(_communityRepoSettings.CommunityProfilesRepoBranch)
            ? "main"
            : _communityRepoSettings.CommunityProfilesRepoBranch.Trim();

    public async Task<List<CommunityTemplateInfo>> GetTemplatesAsync()
    {
        if (DateTime.Now - _lastRequestTime < MinRequestInterval)
        {
            App.Logger.Warning("Request throttled: Refreshing too fast.");
            return new List<CommunityTemplateInfo>();
        }
        _lastRequestTime = DateTime.Now;

        var list = await LoadCommunityIndexFromNetworkAsync(CancellationToken.None);
        return list ?? [];
    }

    public Task<List<CommunityTemplateInfo>?> GetCommunityIndexSnapshotAsync(CancellationToken cancellationToken = default)
        => LoadCommunityIndexFromNetworkAsync(cancellationToken);

    private async Task<List<CommunityTemplateInfo>?> LoadCommunityIndexFromNetworkAsync(CancellationToken cancellationToken)
    {
        try
        {
            App.Logger.Info("Fetching community index with fallback strategy...");
            var json = await DownloadStringWithFallbackAsync("index.json");

            if (string.IsNullOrEmpty(json))
            {
                App.Logger.Error("Failed to fetch community index from both GitHub and CDN.");
                return null;
            }

            var templates = JsonConvert.DeserializeObject<List<CommunityTemplateInfo>>(json) ?? new List<CommunityTemplateInfo>();

            foreach (var t in templates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relativePath = ResolveRelativePath(t);
                if (!string.IsNullOrWhiteSpace(relativePath))
                {
                    t.RelativePath = relativePath;
                    var (catalogFolder, fileName) = SplitRelativePath(relativePath);
                    t.CatalogFolder = catalogFolder;
                    t.FileName = fileName;
                }
                else if (!string.IsNullOrWhiteSpace(t.CatalogFolder) && !string.IsNullOrWhiteSpace(t.FileName))
                {
                    t.RelativePath = JoinCatalogAndFile(t.CatalogFolder, t.FileName);
                }

                t.DownloadUrl = GetEffectiveDownloadUrl(t);
            }

            App.Logger.Info($"Successfully loaded {templates.Count} templates.");
            return templates;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            App.Logger.Error("Unexpected error while fetching community index", ex);
            return null;
        }
    }

    public async Task<bool> DownloadTemplateAsync(CommunityTemplateInfo template)
        => await DownloadTemplateAsync(template, allowOverwrite: true);

    public Task<bool> IsTemplateDownloadedAsync(CommunityTemplateInfo template)
    {
        try
        {
            if (template is null || string.IsNullOrWhiteSpace(template.Id))
                return Task.FromResult(false);

            var candidatePath = ResolveLocalTemplatePath(template);
            if (string.IsNullOrWhiteSpace(candidatePath))
                return Task.FromResult(false);

            return Task.FromResult(_localFileService.FileExists(candidatePath));
        }
        catch (Exception ex)
        {
            App.Logger.Warning($"Local install check failed for template '{template?.Id}': {ex.Message}");
            return Task.FromResult(false);
        }
    }

    public Task<CommunityTemplateDownloadPrecheckResult> CheckLocalTemplateConflictAsync(CommunityTemplateInfo template)
    {
        try
        {
            if (template is null || string.IsNullOrWhiteSpace(template.Id))
                return Task.FromResult(new CommunityTemplateDownloadPrecheckResult(false, null));

            var candidatePath = ResolveLocalTemplatePath(template);
            if (string.IsNullOrWhiteSpace(candidatePath))
                return Task.FromResult(new CommunityTemplateDownloadPrecheckResult(false, null));

            if (!_localFileService.FileExists(candidatePath))
                return Task.FromResult(new CommunityTemplateDownloadPrecheckResult(false, null));

            var (_, fileStem) = ResolveCatalogAndStem(template);
            var existingJson = _localFileService.ReadAllText(candidatePath);
            var existingTemplate = JsonConvert.DeserializeObject<GameProfileTemplate>(existingJson);
            if (existingTemplate is null)
                return Task.FromResult(new CommunityTemplateDownloadPrecheckResult(false, null));

            var sameId = string.Equals(
                (existingTemplate.ProfileId ?? string.Empty).Trim(),
                fileStem,
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

    public Task<bool> DeleteLocalTemplateAsync(CommunityTemplateInfo template)
    {
        try
        {
            if (template is null || string.IsNullOrWhiteSpace(template.Id))
                return Task.FromResult(false);

            var candidatePath = ResolveLocalTemplatePath(template);
            if (string.IsNullOrWhiteSpace(candidatePath) || !_localFileService.FileExists(candidatePath))
                return Task.FromResult(false);

            var (catalogFolder, fileStem) = ResolveCatalogAndStem(template);
            if (string.IsNullOrWhiteSpace(fileStem))
                return Task.FromResult(false);

            var storageKey = TemplateStorageKey.Format(catalogFolder, fileStem);
            _profileService.DeleteTemplate(storageKey);
            _profileService.ReloadTemplates(fileStem);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            App.Logger.Error($"Failed to delete local template {template?.DisplayName}", ex);
            return Task.FromResult(false);
        }
    }

    public async Task<bool> DownloadTemplateAsync(CommunityTemplateInfo template, bool allowOverwrite = true)
    {
        try
        {
            App.Logger.Info($"Downloading template: {template.DisplayName} using URL: {template.DownloadUrl}");

            var request = CreateRequest(template);
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

            var (catalogFolder, _) = ResolveCatalogAndStem(template);
            profileTemplate.TemplateCatalogFolder = catalogFolder;
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

    private string GetEffectiveDownloadUrl(CommunityTemplateInfo template)
        => _useCdnPreferred ? GetCdnUrl(template) : GetGitHubRawUrl(template);

    private GitHubRepositoryContentRequest CreateRequest(CommunityTemplateInfo template)
    {
        var relativePath = ResolveRelativePath(template);
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            var (folder, stem) = ResolveCatalogAndStem(template);
            relativePath = JoinCatalogAndFile(folder, $"{stem}.json");
        }
        return new GitHubRepositoryContentRequest(RepoOwner, RepoName, Branch, relativePath);
    }

    private string GetGitHubRawUrl(CommunityTemplateInfo template) =>
        _gitHubContentService.BuildRawUrl(CreateRequest(template));

    private string GetCdnUrl(CommunityTemplateInfo template) =>
        _gitHubContentService.BuildCdnUrl(CreateRequest(template));

    private (string? catalogFolder, string fileStem) ResolveCatalogAndStem(CommunityTemplateInfo template)
    {
        var relativePath = ResolveRelativePath(template);
        if (!string.IsNullOrWhiteSpace(relativePath))
        {
            var (catalogFolder, fileName) = SplitRelativePath(relativePath);
            var stem = Path.GetFileNameWithoutExtension(fileName)?.Trim() ?? string.Empty;
            if (stem.Length > 0)
                return (catalogFolder, stem);
        }

        var fallbackStem = (template.Id ?? string.Empty).Trim();
        var fallbackFolder = NormalizeCatalogFolder(template.CatalogFolder);
        return (fallbackFolder.Length == 0 ? null : fallbackFolder, fallbackStem);
    }

    private string ResolveLocalTemplatePath(CommunityTemplateInfo template)
    {
        var (catalogFolder, fileStem) = ResolveCatalogAndStem(template);
        if (fileStem.Length == 0)
            return string.Empty;

        var templatesRoot = _profileService.LoadTemplateDirectory();
        return AppPaths.TemplateCatalogPaths.GetTemplateJsonPath(
            templatesRoot,
            catalogFolder,
            fileStem);
    }

    private string ResolveRelativePath(CommunityTemplateInfo template)
    {
        var explicitRelative = NormalizeRelativePath(template.RelativePath);
        if (explicitRelative.Length > 0)
            return explicitRelative;

        var folder = NormalizeCatalogFolder(template.CatalogFolder);
        var fileName = NormalizeFileName(template.FileName);
        if (folder.Length > 0 && fileName.Length > 0)
            return JoinCatalogAndFile(folder, fileName);

        var id = (template.Id ?? string.Empty).Trim();
        if (folder.Length > 0 && id.Length > 0)
            return JoinCatalogAndFile(folder, $"{id}.json");

        var fromUrl = ExtractRepoRelativePathFromUrl(template.DownloadUrl);
        if (fromUrl.Length > 0)
            return fromUrl;

        return string.Empty;
    }

    private string ExtractRepoRelativePathFromUrl(string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl) || !Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri))
            return string.Empty;

        var path = uri.AbsolutePath.Trim('/');
        if (path.Length == 0)
            return string.Empty;

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
            return string.Empty;

        if (uri.Host.Contains("raw.githubusercontent.com", StringComparison.OrdinalIgnoreCase))
        {
            if (segments.Length < 4)
                return string.Empty;
            return NormalizeRelativePath(string.Join('/', segments, 3, segments.Length - 3));
        }

        if (uri.Host.Contains("jsdelivr.net", StringComparison.OrdinalIgnoreCase))
        {
            if (segments.Length < 4
                || !string.Equals(segments[0], "gh", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return NormalizeRelativePath(string.Join('/', segments, 3, segments.Length - 3));
        }

        return NormalizeRelativePath(path);
    }

    private static (string? catalogFolder, string fileName) SplitRelativePath(string relativePath)
    {
        var normalized = NormalizeRelativePath(relativePath);
        var slash = normalized.LastIndexOf('/');
        if (slash < 0)
            return (null, normalized);

        var folder = normalized[..slash].Trim();
        var file = normalized[(slash + 1)..].Trim();
        return (folder.Length == 0 ? null : folder, file);
    }

    private static string NormalizeRelativePath(string? raw)
        => string.Join('/',
            (raw ?? string.Empty)
                .Replace('\\', '/')
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static string NormalizeCatalogFolder(string? raw)
        => NormalizeRelativePath(raw);

    private static string NormalizeFileName(string? raw)
        => Path.GetFileName((raw ?? string.Empty).Trim())?.Trim() ?? string.Empty;

    private static string JoinCatalogAndFile(string? catalogFolder, string fileName)
    {
        var folder = NormalizeCatalogFolder(catalogFolder);
        var file = NormalizeFileName(fileName);
        if (file.Length == 0)
            return string.Empty;
        return folder.Length == 0 ? file : $"{folder}/{file}";
    }
}


