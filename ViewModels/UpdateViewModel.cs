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

    public string DownloadPrimaryActionText => DownloadFailed ? "Retry download" : "Download New Version";

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
        IUpdateInstallerService? updateInstallerService = null)
    {
        _updateService = updateService;
        _localFileService = localFileService ?? new LocalFileService();
        _settingsService = settingsService;
        _appSettings = appSettings;
        _updateInstallerService = updateInstallerService ?? new UpdateInstallerService();

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

        IsChecking = true;
        IsForbidden = false;
        StatusMessage = "Checking for updates...";
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

            StatusMessage = info.ErrorMessage ?? (IsUpdateAvailable ? "New version available!" : "You are up to date.");
            DownloadFailed = false;
            DownloadUpdateCommand.NotifyCanExecuteChanged();
            InstallUpdateCommand.NotifyCanExecuteChanged();
        }
        catch
        {
            StatusMessage = "Failed to check for updates.";
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

        IsDownloading = true;
        DownloadFailed = false;
        DownloadProgressPercent = 0;
        DownloadSpeedText = "--";
        DownloadEtaText = "--";
        _activeDownloadFilePath = null;
        StatusMessage = "Preparing download...";
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
                    $"A local file with the same name already exists.\n\n{fileName}\n\nDo you want to overwrite it?",
                    "Overwrite Existing File",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);
                if (overwrite != System.Windows.MessageBoxResult.Yes)
                {
                    StatusMessage = $"Download canceled for {fileName}.";
                    return;
                }
            }

            _activeDownloadFilePath = targetPath;
            var progress = new Progress<ReleaseDownloadProgress>(OnDownloadProgressChanged);

            StatusMessage = $"Downloading {fileName}...";
            await _updateService.DownloadReleaseAssetAsync(
                resolution.MatchedAsset.DownloadUrl,
                targetPath,
                progress,
                _downloadCts.Token);

            DownloadProgressPercent = 100;
            DownloadEtaText = IsChineseUi() ? "已完成" : "Done";
            StatusMessage = $"Download complete: {fileName}";
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
            StatusMessage = "Download cancelled.";
            TryDeleteFileIfExists(_activeDownloadFilePath);
            _activeDownloadFilePath = null;
            DownloadProgressPercent = 0;
        }
        catch (Exception ex)
        {
            DownloadFailed = true;
            DownloadProgressPercent = 0;
            StatusMessage = $"Download failed: {ex.Message}";
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
            return IsChineseUi() ? "即将完成" : "Almost there";

        var rounded = TimeSpan.FromSeconds(Math.Ceiling(remaining.TotalSeconds));
        if (IsChineseUi())
        {
            if (rounded.TotalHours >= 1)
                return $"约 {(int)rounded.TotalHours} 小时 {rounded.Minutes} 分";
            if (rounded.TotalMinutes >= 1)
                return $"约 {(int)rounded.TotalMinutes} 分 {rounded.Seconds} 秒";
            return $"约 {Math.Max(1, rounded.Seconds)} 秒";
        }

        if (rounded.TotalHours >= 1)
            return $"about {(int)rounded.TotalHours}h {rounded.Minutes}m";
        if (rounded.TotalMinutes >= 1)
            return $"about {(int)rounded.TotalMinutes}m {rounded.Seconds}s";
        return $"about {Math.Max(1, rounded.Seconds)}s";
    }

    private bool IsChineseUi()
    {
        var uiCulture = (_appSettings.UiCulture ?? string.Empty).Trim();
        return uiCulture.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
    }

    private void PromptInstallDownloadedPackage(string zipPath, string packageName)
    {
        var title = IsChineseUi() ? "安装新版本" : "Install New Version";
        var message = IsChineseUi()
            ? $"已下载完成：{packageName}\n\n是否现在安装？安装时程序将自动退出。"
            : $"Download completed: {packageName}\n\nInstall now? The app will close automatically.";

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
            StatusMessage = IsChineseUi() ? "安装包不存在，请重新下载。" : "Package file not found. Please download again.";
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
        var title = IsChineseUi() ? "安装新版本" : "Install New Version";
        if (askConfirmation)
        {
            var message = IsChineseUi()
                ? $"准备安装：{packageName}\n\n继续后程序将退出并安装更新，是否继续？"
                : $"Ready to install: {packageName}\n\nThe app will close and apply update. Continue?";
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
            var errorText = IsChineseUi()
                ? $"启动安装脚本失败：{errorMessage ?? "未知错误"}"
                : $"Failed to start installer script: {errorMessage ?? "Unknown error"}";
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
        return version;
    }
}
