using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamepadMapperGUI.Core;
using GamepadMapperGUI.Interfaces.Services;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Models.Core;
using GamepadMapperGUI.Services;
using GamepadMapperGUI.Utils;
using System.Collections.Generic;
using System.Windows;

namespace Gamepad_Mapping.ViewModels;

public partial class UpdateViewModel : ObservableObject
{
    private static readonly IReadOnlyList<string> PreservedInstallDirectories = new[] { "Assets", "Config" };

    private readonly IUpdateService _updateService;
    private readonly ILocalFileService _localFileService;
    private readonly ISettingsService _settingsService;
    private readonly AppSettings _appSettings;
    private readonly IUpdateInstallerService _updateInstallerService;
    private readonly IUpdateQuotaService _updateQuotaService;
    private readonly IUpdateVersionCacheService _updateVersionCacheService;

    [ObservableProperty]
    private string _currentVersion = "1.0.0";

    [ObservableProperty]
    private string? _latestVersion;

    [ObservableProperty]
    private bool _isUpdateAvailable;

    [ObservableProperty]
    private bool _isChecking;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isForbidden;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private double _downloadProgressPercent;

    [ObservableProperty]
    private string _downloadSpeedText = "--";

    [ObservableProperty]
    private string _downloadEtaText = "--";

    [ObservableProperty]
    private string _installModeText = "Unknown";

    [ObservableProperty]
    private bool _downloadFailed;

    public string DownloadPrimaryActionText => DownloadFailed 
        ? GetLoc("UpdateDownloadRetry") 
        : GetLoc("UpdateDownloadNewVersion");

    private string GetLoc(string key)
    {
        if (Application.Current?.Resources["Loc"] is TranslationService loc)
            return loc[key];
        return key;
    }

    public bool IncludePrereleases
    {
        get => _appSettings.IncludePrereleases;
        set
        {
            if (_appSettings.IncludePrereleases != value)
            {
                _appSettings.IncludePrereleases = value;
                _settingsService.SaveSettings(_appSettings);
                OnPropertyChanged();
                _ = CheckForUpdatesAsync();
            }
        }
    }

    public IAsyncRelayCommand CheckUpdateCommand { get; }
    public IAsyncRelayCommand DownloadUpdateCommand { get; }
    public IRelayCommand InstallUpdateCommand { get; }
    public IRelayCommand CancelDownloadCommand { get; }
    public ICommand OpenReleaseUrlCommand { get; }
    public ICommand OpenManualUrlCommand { get; }

    private string? _releaseUrl;
    private CancellationTokenSource? _downloadCts;
    private string? _activeDownloadFilePath;
    private string? _lastDownloadedPackagePath;
    private string? _lastDownloadedPackageName;
    private string? _lastDownloadedPackageSha256;

    public UpdateViewModel(
        IUpdateService updateService,
        ISettingsService settingsService,
        AppSettings appSettings,
        ILocalFileService? localFileService = null,
        IUpdateInstallerService? updateInstallerService = null,
        IUpdateQuotaService? updateQuotaService = null,
        IUpdateVersionCacheService? updateVersionCacheService = null)
    {
        _updateService = updateService;
        _localFileService = localFileService ?? new LocalFileService();
        _settingsService = settingsService;
        _appSettings = appSettings;
        _updateInstallerService = updateInstallerService ?? new UpdateInstallerService();
        _updateQuotaService = updateQuotaService ?? new UpdateQuotaService(new StaticUpdateQuotaPolicyProvider());
        _updateVersionCacheService = updateVersionCacheService ?? new UpdateVersionCacheService();

        CheckUpdateCommand = new AsyncRelayCommand(CheckForUpdatesAsync);
        DownloadUpdateCommand = new AsyncRelayCommand(DownloadUpdateAsync, CanDownloadUpdate);
        InstallUpdateCommand = new RelayCommand(InstallDownloadedPackage, CanInstallDownloadedPackage);
        CancelDownloadCommand = new RelayCommand(CancelDownload, () => IsDownloading);
        OpenReleaseUrlCommand = new RelayCommand(OpenReleaseUrl);
        OpenManualUrlCommand = new RelayCommand(OpenManualUrl);

        _currentVersion = GetCurrentVersion();
        InstallModeText = ToInstallModeText(AppInstallModeDetector.DetectCurrent());
        RefreshLocalInstallPackageCandidate();
    }

    partial void OnDownloadFailedChanged(bool value) => OnPropertyChanged(nameof(DownloadPrimaryActionText));

    partial void OnIsDownloadingChanged(bool value)
    {
        CancelDownloadCommand.NotifyCanExecuteChanged();
        DownloadUpdateCommand.NotifyCanExecuteChanged();
        InstallUpdateCommand.NotifyCanExecuteChanged();
    }

    private void CancelDownload() => _downloadCts?.Cancel();

    private async Task CheckForUpdatesAsync()
    {
        if (IsChecking) return;
        var quotaDecision = await _updateQuotaService.TryConsumeQuotaAsync(UpdateQuotaAction.Check);
        if (!quotaDecision.IsAllowed)
        {
            StatusMessage = BuildQuotaBlockedMessage(quotaDecision);
            return;
        }

        IsChecking = true;
        IsForbidden = false;
        StatusMessage = GetLoc("UpdateChecking");
        LatestVersion = null;
        DownloadUpdateCommand.NotifyCanExecuteChanged();
        InstallUpdateCommand.NotifyCanExecuteChanged();

        try
        {
            var info = await _updateService.CheckForUpdatesAsync(
                _appSettings.GithubRepoOwner, 
                _appSettings.GithubRepoName,
                IncludePrereleases);
            
            CurrentVersion = info.CurrentVersion;
            LatestVersion = info.LatestVersion;
            IsUpdateAvailable = info.IsUpdateAvailable;
            _releaseUrl = info.ReleaseUrl;
            IsForbidden = info.IsForbidden;

            StatusMessage = BuildUpdateStatusMessage(info);
            DownloadFailed = false;
            DownloadUpdateCommand.NotifyCanExecuteChanged();
            InstallUpdateCommand.NotifyCanExecuteChanged();
        }
        catch
        {
            StatusMessage = BuildUpdateCheckFailedMessageWithCacheFallback();
        }
        finally
        {
            IsChecking = false;
            RefreshLocalInstallPackageCandidate();
            DownloadUpdateCommand.NotifyCanExecuteChanged();
            InstallUpdateCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanDownloadUpdate() => IsUpdateAvailable && !IsChecking && !IsDownloading;
    private bool CanInstallDownloadedPackage() =>
        !IsChecking &&
        !IsDownloading &&
        !string.IsNullOrWhiteSpace(_lastDownloadedPackagePath) &&
        _localFileService.FileExists(_lastDownloadedPackagePath);

    private async Task DownloadUpdateAsync()
    {
        if (IsDownloading) return;
        var quotaDecision = await _updateQuotaService.TryConsumeQuotaAsync(UpdateQuotaAction.Download);
        if (!quotaDecision.IsAllowed)
        {
            StatusMessage = BuildQuotaBlockedMessage(quotaDecision);
            return;
        }

        IsDownloading = true;
        DownloadFailed = false;
        DownloadProgressPercent = 0;
        DownloadSpeedText = "--";
        DownloadEtaText = "--";
        _activeDownloadFilePath = null;
        StatusMessage = GetLoc("UpdatePreparingDownload");
        DownloadUpdateCommand.NotifyCanExecuteChanged();

        _downloadCts?.Dispose();
        _downloadCts = new CancellationTokenSource();

        try
        {
            var installMode = AppInstallModeDetector.DetectCurrent();
            InstallModeText = ToInstallModeText(installMode);

            var resolution = await _updateService.ResolveReleaseAssetAsync(
                _appSettings.GithubRepoOwner,
                _appSettings.GithubRepoName,
                IncludePrereleases,
                installMode,
                _downloadCts.Token);

            _releaseUrl = resolution.ReleasePageUrl ?? _releaseUrl;
            if (resolution.MatchedAsset is null)
            {
                DownloadFailed = true;
                StatusMessage = resolution.ErrorMessage ?? "No downloadable package available for current installation mode.";
                return;
            }

            var fileName = resolution.MatchedAsset.Name;
            if (string.IsNullOrWhiteSpace(fileName))
                fileName = $"Gamepad-Mapping-{resolution.VersionTag ?? "latest"}.zip";

            var targetPath = Path.Combine(AppPaths.GetUpdateDownloadsDirectory(), fileName);
            if (_localFileService.FileExists(targetPath))
            {
                var overwrite = System.Windows.MessageBox.Show(
                    string.Format(GetLoc("UpdateOverwriteExistingFilePrompt"), fileName),
                    GetLoc("UpdateOverwriteExistingFile"),
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);
                if (overwrite != System.Windows.MessageBoxResult.Yes)
                {
                    StatusMessage = string.Format(GetLoc("UpdateDownloadCanceled"), fileName);
                    return;
                }
            }

            _activeDownloadFilePath = targetPath;
            var progress = new Progress<ReleaseDownloadProgress>(OnDownloadProgressChanged);

            StatusMessage = string.Format(GetLoc("UpdateDownloading"), fileName);
            await _updateService.DownloadReleaseAssetAsync(
                resolution.MatchedAsset.DownloadUrl,
                targetPath,
                progress,
                _downloadCts.Token);

            DownloadProgressPercent = 100;
            DownloadEtaText = GetLoc("UpdateDownloadDone");
            StatusMessage = string.Format(GetLoc("UpdateDownloadComplete"), fileName);
            _activeDownloadFilePath = null;
            _lastDownloadedPackagePath = targetPath;
            _lastDownloadedPackageName = fileName;
            _lastDownloadedPackageSha256 = string.IsNullOrWhiteSpace(resolution.MatchedAsset.Sha256)
                ? ComputeFileSha256(targetPath)
                : resolution.MatchedAsset.Sha256;
            InstallUpdateCommand.NotifyCanExecuteChanged();
            PromptInstallDownloadedPackage(targetPath, fileName);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = GetLoc("UpdateDownloadCancelled");
            TryDeleteFileIfExists(_activeDownloadFilePath);
            _activeDownloadFilePath = null;
            DownloadProgressPercent = 0;
        }
        catch (Exception ex)
        {
            DownloadFailed = true;
            DownloadProgressPercent = 0;
            StatusMessage = string.Format(GetLoc("UpdateDownloadFailed"), ex.Message);
        }
        finally
        {
            IsDownloading = false;
            DownloadUpdateCommand.NotifyCanExecuteChanged();
            InstallUpdateCommand.NotifyCanExecuteChanged();
        }
    }

    private void OnDownloadProgressChanged(ReleaseDownloadProgress progress)
    {
        if (progress.TotalBytes is > 0)
        {
            DownloadProgressPercent = Math.Clamp(progress.BytesReceived * 100d / progress.TotalBytes.Value, 0, 100);
        }
        else
        {
            DownloadProgressPercent = 0;
        }

        DownloadSpeedText = FormatSpeed(progress.BytesPerSecond);
        DownloadEtaText = progress.EstimatedRemaining.HasValue
            ? FormatTimeLeft(progress.EstimatedRemaining.Value)
            : "--";
    }

    private void OpenReleaseUrl() => OpenUrl(_releaseUrl ?? GetBaseReleaseUrl("latest"));
    private void OpenManualUrl() => OpenUrl(GetBaseReleaseUrl());

    private string GetBaseReleaseUrl(string suffix = "")
    {
        var baseUrl = $"https://github.com/{_appSettings.GithubRepoOwner}/{_appSettings.GithubRepoName}/releases";
        return string.IsNullOrEmpty(suffix) ? baseUrl : $"{baseUrl}/{suffix}";
    }

    private void OpenUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return;
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch { /* Ignore */ }
    }

    private static string ToInstallModeText(AppInstallMode mode) => mode switch
    {
        AppInstallMode.Fx => "fx",
        AppInstallMode.Single => "single",
        _ => "unknown"
    };

    private string FormatSpeed(double bytesPerSecond)
    {
        if (bytesPerSecond <= 1d) return "--";

        const double b = 1d;
        const double kb = 1024d;
        const double mb = kb * 1024d;
        const double gb = mb * 1024d;
        var culture = IsChineseUi() ? CultureInfo.GetCultureInfo("zh-CN") : CultureInfo.GetCultureInfo("en-US");

        if (bytesPerSecond >= gb) return $"{(bytesPerSecond / gb).ToString("0.00", culture)} GB/s";
        if (bytesPerSecond >= mb) return $"{(bytesPerSecond / mb).ToString("0.00", culture)} MB/s";
        if (bytesPerSecond >= kb) return $"{(bytesPerSecond / kb).ToString("0.00", culture)} KB/s";
        return $"{Math.Round(bytesPerSecond / b):0} B/s";
    }

    private string FormatTimeLeft(TimeSpan remaining)
    {
        if (remaining <= TimeSpan.Zero)
            return GetLoc("UpdateAlmostThere");

        var rounded = TimeSpan.FromSeconds(Math.Ceiling(remaining.TotalSeconds));
        if (rounded.TotalHours >= 1)
            return string.Format(GetLoc("UpdateAboutHoursMinutes"), (int)rounded.TotalHours, rounded.Minutes);
        if (rounded.TotalMinutes >= 1)
            return string.Format(GetLoc("UpdateAboutMinutesSeconds"), (int)rounded.TotalMinutes, rounded.Seconds);
        return string.Format(GetLoc("UpdateAboutSeconds"), Math.Max(1, rounded.Seconds));
    }

    private bool IsChineseUi()
    {
        var uiCulture = (_appSettings.UiCulture ?? string.Empty).Trim();
        return uiCulture.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
    }

    private void PromptInstallDownloadedPackage(string zipPath, string packageName)
    {
        var title = GetLoc("UpdateInstallTitle");
        var message = string.Format(GetLoc("UpdateInstallPromptNow"), packageName);

        var installNow = MessageBox.Show(
            message,
            title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (installNow != MessageBoxResult.Yes)
            return;

        InstallPackage(zipPath, packageName, askConfirmation: false);
    }

    private void InstallDownloadedPackage()
    {
        var packagePath = _lastDownloadedPackagePath;
        if (string.IsNullOrWhiteSpace(packagePath) || !_localFileService.FileExists(packagePath))
            RefreshLocalInstallPackageCandidate();

        packagePath = _lastDownloadedPackagePath;
        if (string.IsNullOrWhiteSpace(packagePath))
            return;

        if (!_localFileService.FileExists(packagePath))
        {
            StatusMessage = GetLoc("UpdateInstallPackageNotFound");
            InstallUpdateCommand.NotifyCanExecuteChanged();
            return;
        }

        var packageName = string.IsNullOrWhiteSpace(_lastDownloadedPackageName)
            ? Path.GetFileName(packagePath)
            : _lastDownloadedPackageName;
        InstallPackage(packagePath, packageName ?? "update.zip", askConfirmation: true);
    }

    private void InstallPackage(string zipPath, string packageName, bool askConfirmation)
    {
        var title = GetLoc("UpdateInstallTitle");
        if (askConfirmation)
        {
            var message = string.Format(GetLoc("UpdateInstallReadyPrompt"), packageName);
            var proceed = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (proceed != MessageBoxResult.Yes)
                return;
        }

        var request = new UpdateInstallRequest(
            ZipPackagePath: zipPath,
            TargetDirectoryPath: AppPaths.ResolveContentRoot(),
            AppExecutablePath: Environment.ProcessPath ?? string.Empty,
            PreserveDirectoryNames: PreservedInstallDirectories,
            ProcessIdToWaitFor: Environment.ProcessId,
            ExpectedZipSha256: _lastDownloadedPackageSha256,
            InstallLogPath: BuildInstallLogPath(),
            RemoveOrphanFiles: true);

        if (!_updateInstallerService.TryLaunchInstaller(request, out var errorMessage))
        {
            var errorText = string.Format(GetLoc("UpdateInstallLaunchFailed"), errorMessage ?? "Unknown error");
            MessageBox.Show(errorText, title, MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        Application.Current?.Shutdown();
    }

    private static string BuildInstallLogPath()
    {
        var logsDir = AppPaths.GetLogsDirectory();
        var fileName = $"update-install-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.log";
        return Path.Combine(logsDir, fileName);
    }

    private static string? ComputeFileSha256(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var hash = SHA256.HashData(stream);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
        catch
        {
            return null;
        }
    }

    private void RefreshLocalInstallPackageCandidate()
    {
        try
        {
            var updatesDir = AppPaths.GetUpdateDownloadsDirectory();
            if (!Directory.Exists(updatesDir))
                return;

            var newestZip = new DirectoryInfo(updatesDir)
                .EnumerateFiles("*.zip", SearchOption.TopDirectoryOnly)
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .FirstOrDefault();

            if (newestZip is null)
                return;

            _lastDownloadedPackagePath = newestZip.FullName;
            _lastDownloadedPackageName = newestZip.Name;
            _lastDownloadedPackageSha256 ??= ComputeFileSha256(newestZip.FullName);
        }
        catch
        {
            // Ignore local package discovery failures.
        }
    }

    private void TryDeleteFileIfExists(string? path)
    {
        try
        {
            _localFileService.DeleteFileIfExists(path);
        }
        catch
        {
            // Ignore.
        }
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
        if (version.Contains('+'))
        {
            version = version.Split('+')[0];
        }

        return version;
    }

    private string BuildQuotaBlockedMessage(UpdateQuotaDecision decision)
    {
        return decision.BlockReason switch
        {
            UpdateQuotaBlockReason.Cooldown => BuildCooldownBlockedMessage(decision),
            UpdateQuotaBlockReason.DailyLimit => BuildDailyLimitBlockedMessage(decision),
            _ => GetLoc("UpdateCheckFailed")
        };
    }

    private string BuildCooldownBlockedMessage(UpdateQuotaDecision decision)
    {
        var retryAfter = decision.RetryAfter ?? TimeSpan.Zero;
        var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
        return IsChineseUi()
            ? $"请求过于频繁，请在 {seconds} 秒后重试。"
            : $"Too many requests. Please retry in {seconds} seconds.";
    }

    private string BuildDailyLimitBlockedMessage(UpdateQuotaDecision decision)
    {
        var actionText = decision.Action == UpdateQuotaAction.Download
            ? (IsChineseUi() ? "下载更新包" : "download update packages")
            : (IsChineseUi() ? "检查更新" : "check for updates");
        return IsChineseUi()
            ? $"今日{actionText}次数已达上限（{decision.DailyLimit} 次）。请明天再试。"
            : $"Daily limit reached for {actionText} ({decision.DailyLimit} per day). Please try again tomorrow.";
    }

    private string BuildUpdateCheckFailedMessageWithCacheFallback()
    {
        return BuildStatusMessageWithCachedVersionHint(GetLoc("UpdateCheckFailed"));
    }

    private string BuildUpdateStatusMessage(AppUpdateInfo info)
    {
        if (!string.IsNullOrWhiteSpace(info.ErrorMessage))
            return BuildStatusMessageWithCachedVersionHint(info.ErrorMessage);

        return IsUpdateAvailable ? GetLoc("UpdateNewVersionAvailable") : GetLoc("UpdateUpToDate");
    }

    private string BuildStatusMessageWithCachedVersionHint(string baseMessage)
    {
        var cached = _updateVersionCacheService.TryGetLatestVersion(_appSettings.GithubRepoOwner, _appSettings.GithubRepoName);
        if (cached is null)
            return baseMessage;

        var hint = string.Format(
            GetLoc("UpdateCheckFailedCachedLatestHint"),
            cached.LatestVersion,
            FormatCacheUtcTime(cached.CachedAtUtc));
        return $"{baseMessage} {hint}";
    }

    private static string FormatCacheUtcTime(DateTimeOffset utcTime)
    {
        if (utcTime <= DateTimeOffset.MinValue)
            return "--";
        return utcTime.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss");
    }
}
