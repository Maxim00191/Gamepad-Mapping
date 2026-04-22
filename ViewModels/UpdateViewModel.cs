using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamepadMapperGUI.Core;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Storage;
using GamepadMapperGUI.Interfaces.Services.Update;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Models.Core;
using GamepadMapperGUI.Services.Infrastructure;
using GamepadMapperGUI.Services.Storage;
using GamepadMapperGUI.Services.Update;
using GamepadMapperGUI.Utils;
using System.Collections.Generic;
using System.Windows;

namespace Gamepad_Mapping.ViewModels;

public partial class UpdateViewModel : ObservableObject, IDisposable
{
    private readonly IUpdateService _updateService;
    private readonly ILocalFileService _localFileService;
    private readonly ISettingsService _settingsService;
    private readonly AppSettings _appSettings;
    private readonly IUpdateInstallerService _updateInstallerService;
    private readonly IUpdateQuotaService _updateQuotaService;
    private readonly IUpdateVersionCacheService _updateVersionCacheService;
    private readonly IUserDialogService _userDialogService;

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
    private string _installModeText;

    [ObservableProperty]
    private bool _downloadFailed;

    public string DownloadPrimaryActionText => DownloadFailed
        ? AppUiLocalization.GetString("UpdateDownloadRetry")
        : (IsDownloading ? AppUiLocalization.GetString("UpdateCancelButton") : AppUiLocalization.GetString("UpdateDownloadNewVersion"));

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
    public IRelayCommand DownloadOrStopCommand { get; }
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
    private string? _lastDownloadedReleaseTag;

    public UpdateViewModel(
        IUpdateService updateService,
        ISettingsService settingsService,
        AppSettings appSettings,
        ILocalFileService? localFileService = null,
        IUpdateInstallerService? updateInstallerService = null,
        IUpdateQuotaService? updateQuotaService = null,
        IUpdateVersionCacheService? updateVersionCacheService = null,
        IUserDialogService? userDialogService = null)
    {
        _updateService = updateService;
        _localFileService = localFileService ?? new LocalFileService();
        _settingsService = settingsService;
        _appSettings = appSettings;
        _updateInstallerService = updateInstallerService ?? new UpdateInstallerService();
        _updateQuotaService = updateQuotaService ?? new UpdateQuotaService(new StaticUpdateQuotaPolicyProvider());
        _updateVersionCacheService = updateVersionCacheService ?? new UpdateVersionCacheService();
        _userDialogService = userDialogService ?? new UserDialogService();

        CheckUpdateCommand = new AsyncRelayCommand(CheckForUpdatesAsync);
        DownloadUpdateCommand = new AsyncRelayCommand(DownloadUpdateAsync, CanDownloadUpdate);
        DownloadOrStopCommand = new RelayCommand(DownloadOrStop, CanDownloadOrStop);
        InstallUpdateCommand = new RelayCommand(InstallDownloadedPackage, CanInstallDownloadedPackage);
        CancelDownloadCommand = new RelayCommand(CancelDownload, () => IsDownloading);
        OpenReleaseUrlCommand = new RelayCommand(OpenReleaseUrl);
        OpenManualUrlCommand = new RelayCommand(OpenManualUrl);

        _currentVersion = GetCurrentVersion();
        InstallModeText = ToInstallModeText(AppInstallModeDetector.DetectCurrent());
        RefreshLocalInstallPackageCandidate();
    }

    partial void OnDownloadFailedChanged(bool value) => OnPropertyChanged(nameof(DownloadPrimaryActionText));

    partial void OnIsUpdateAvailableChanged(bool value)
    {
        DownloadUpdateCommand.NotifyCanExecuteChanged();
        DownloadOrStopCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsCheckingChanged(bool value)
    {
        DownloadUpdateCommand.NotifyCanExecuteChanged();
        DownloadOrStopCommand.NotifyCanExecuteChanged();
        InstallUpdateCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsDownloadingChanged(bool value)
    {
        CancelDownloadCommand.NotifyCanExecuteChanged();
        DownloadUpdateCommand.NotifyCanExecuteChanged();
        DownloadOrStopCommand.NotifyCanExecuteChanged();
        InstallUpdateCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(DownloadPrimaryActionText));
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
        StatusMessage = AppUiLocalization.GetString("UpdateChecking");
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
            AppendNetworkFallbackNoticeIfAny();
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
    private bool CanDownloadOrStop() => IsUpdateAvailable && !IsChecking;
    private bool CanInstallDownloadedPackage() =>
        !IsChecking &&
        !IsDownloading &&
        !string.IsNullOrWhiteSpace(_lastDownloadedPackagePath) &&
        _localFileService.FileExists(_lastDownloadedPackagePath);

    private void DownloadOrStop()
    {
        if (IsDownloading)
        {
            CancelDownload();
            return;
        }
        _ = DownloadUpdateAsync();
    }

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
        StatusMessage = AppUiLocalization.GetString("UpdatePreparingDownload");
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
            AppendNetworkFallbackNoticeIfAny();
            if (resolution.MatchedAsset is null)
            {
                DownloadFailed = true;
                StatusMessage = resolution.ErrorMessage ?? AppUiLocalization.GetString("UpdateNoPackageForInstallMode");
                return;
            }
            if (string.IsNullOrWhiteSpace(resolution.MatchedAsset.Sha256))
            {
                DownloadFailed = true;
                StatusMessage = AppUiLocalization.GetString("UpdateChecksumSignatureValidationFailed");
                return;
            }

            var fileName = resolution.MatchedAsset.Name;
            if (string.IsNullOrWhiteSpace(fileName))
                fileName = $"Gamepad-Mapping-{resolution.VersionTag ?? "latest"}.zip";

            var targetPath = Path.Combine(AppPaths.GetUpdateDownloadsDirectory(), fileName);
            if (_localFileService.FileExists(targetPath))
            {
                var overwrite = _userDialogService.Show(
                    string.Format(AppUiLocalization.GetString("UpdateOverwriteExistingFilePrompt"), fileName),
                    AppUiLocalization.GetString("UpdateOverwriteExistingFile"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (overwrite != MessageBoxResult.Yes)
                {
                    StatusMessage = string.Format(AppUiLocalization.GetString("UpdateDownloadCanceled"), fileName);
                    return;
                }
            }

            _activeDownloadFilePath = targetPath;
            var progress = new Progress<ReleaseDownloadProgress>(OnDownloadProgressChanged);

            StatusMessage = string.Format(AppUiLocalization.GetString("UpdateDownloading"), fileName);
            await _updateService.DownloadReleaseAssetAsync(
                resolution.MatchedAsset.DownloadUrl,
                targetPath,
                progress,
                _downloadCts.Token);
            AppendNetworkFallbackNoticeIfAny();

            DownloadProgressPercent = 100;
            DownloadEtaText = AppUiLocalization.GetString("UpdateDownloadDone");
            StatusMessage = string.Format(AppUiLocalization.GetString("UpdateDownloadComplete"), fileName);
            _activeDownloadFilePath = null;
            _lastDownloadedPackagePath = targetPath;
            _lastDownloadedPackageName = fileName;
            _lastDownloadedPackageSha256 = resolution.MatchedAsset.Sha256;
            _lastDownloadedReleaseTag = resolution.VersionTag;
            InstallUpdateCommand.NotifyCanExecuteChanged();
            PromptInstallDownloadedPackage(targetPath, fileName);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = AppUiLocalization.GetString("UpdateDownloadCancelled");
            TryDeleteFileIfExists(_activeDownloadFilePath);
            _activeDownloadFilePath = null;
            DownloadProgressPercent = 0;
        }
        catch (Exception ex)
        {
            DownloadFailed = true;
            DownloadProgressPercent = 0;
            StatusMessage = string.Format(AppUiLocalization.GetString("UpdateDownloadFailed"), ex.Message);
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

    private string ToInstallModeText(AppInstallMode mode) => mode switch
    {
        AppInstallMode.Fx => AppUiLocalization.GetString("UpdateInstallModeValue_Fx"),
        AppInstallMode.Single => AppUiLocalization.GetString("UpdateInstallModeValue_Single"),
        _ => AppUiLocalization.GetString("UpdateInstallModeValue_Unknown")
    };

    private string FormatSpeed(double bytesPerSecond)
    {
        if (bytesPerSecond <= 1d) return "--";

        const double kb = 1024d;
        const double mb = kb * 1024d;
        const double gb = mb * 1024d;
        var culture = ResolveUiCulture();

        if (bytesPerSecond >= gb)
            return string.Format(
                AppUiLocalization.GetString("UpdateDownloadSpeed_GigabytesPerSecondFormat"),
                (bytesPerSecond / gb).ToString("0.00", culture));
        if (bytesPerSecond >= mb)
            return string.Format(
                AppUiLocalization.GetString("UpdateDownloadSpeed_MegabytesPerSecondFormat"),
                (bytesPerSecond / mb).ToString("0.00", culture));
        if (bytesPerSecond >= kb)
            return string.Format(
                AppUiLocalization.GetString("UpdateDownloadSpeed_KilobytesPerSecondFormat"),
                (bytesPerSecond / kb).ToString("0.00", culture));
        var bytesRounded = Math.Round(bytesPerSecond).ToString("0", culture);
        return string.Format(
            AppUiLocalization.GetString("UpdateDownloadSpeed_BytesPerSecondFormat"),
            bytesRounded);
    }

    private string FormatTimeLeft(TimeSpan remaining)
    {
        if (remaining <= TimeSpan.Zero)
            return AppUiLocalization.GetString("UpdateAlmostThere");

        var rounded = TimeSpan.FromSeconds(Math.Ceiling(remaining.TotalSeconds));
        if (rounded.TotalHours >= 1)
            return string.Format(AppUiLocalization.GetString("UpdateAboutHoursMinutes"), (int)rounded.TotalHours, rounded.Minutes);
        if (rounded.TotalMinutes >= 1)
            return string.Format(AppUiLocalization.GetString("UpdateAboutMinutesSeconds"), (int)rounded.TotalMinutes, rounded.Seconds);
        return string.Format(AppUiLocalization.GetString("UpdateAboutSeconds"), Math.Max(1, rounded.Seconds));
    }

    private static CultureInfo ResolveUiCulture() =>
        AppUiLocalization.TryTranslationService()?.Culture
        ?? CultureInfo.CurrentUICulture;

    private void PromptInstallDownloadedPackage(string zipPath, string packageName)
    {
        var title = AppUiLocalization.GetString("UpdateInstallTitle");
        var message = string.Format(AppUiLocalization.GetString("UpdateInstallPromptNow"), packageName);

        var installNow = _userDialogService.Show(
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
            StatusMessage = AppUiLocalization.GetString("UpdateInstallPackageNotFound");
            InstallUpdateCommand.NotifyCanExecuteChanged();
            return;
        }
        if (string.IsNullOrWhiteSpace(_lastDownloadedPackageSha256))
        {
            StatusMessage = AppUiLocalization.GetString("UpdateSecureChecksumMissing");
            return;
        }

        var packageName = string.IsNullOrWhiteSpace(_lastDownloadedPackageName)
            ? Path.GetFileName(packagePath)
            : _lastDownloadedPackageName;
        InstallPackage(packagePath, packageName ?? "update.zip", askConfirmation: true);
    }

    private void InstallPackage(string zipPath, string packageName, bool askConfirmation)
    {
        var title = AppUiLocalization.GetString("UpdateInstallTitle");
        if (askConfirmation)
        {
            var message = string.Format(AppUiLocalization.GetString("UpdateInstallReadyPrompt"), packageName);
            var proceed = _userDialogService.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (proceed != MessageBoxResult.Yes)
                return;
        }

        var request = new UpdateInstallRequest(
            ZipPackagePath: zipPath,
            TargetDirectoryPath: AppPaths.ResolveContentRoot(),
            AppExecutablePath: Environment.ProcessPath ?? string.Empty,
            PreserveDirectoryNames: BuildPreservePathsForInstall(),
            ProcessIdToWaitFor: Environment.ProcessId,
            TrustedReleaseTag: _lastDownloadedReleaseTag,
            ExpectedZipSha256: _lastDownloadedPackageSha256,
            InstallLogPath: BuildInstallLogPath(),
            RemoveOrphanFiles: ResolveRemoveOrphanFilesPolicy());

        if (!_updateInstallerService.TryLaunchInstaller(request, out var errorMessage))
        {
            // Installation failed
            var failureMessage = string.Format(AppUiLocalization.GetString("UpdateInstallLaunchFailed"), errorMessage ?? AppUiLocalization.GetString("UpdateUnknownError"));
            _userDialogService.Show(failureMessage, title, MessageBoxButton.OK, MessageBoxImage.Error);

            return; // Exit the method as installation failed
        }

        Application.Current?.Shutdown();
    }

    private static string BuildInstallLogPath()
    {
        var logsDir = AppPaths.GetLogsDirectory();
        var fileName = $"update-install-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.log";
        return Path.Combine(logsDir, fileName);
    }

    private IReadOnlyList<string> BuildPreservePathsForInstall()
    {
        var configured = _appSettings.UpdateInstallPolicy?.PreservePaths;
        if (configured is null || configured.Count == 0)
        {
            return
            [
                "Assets/Profiles/templates",
                "Assets/Config/local_settings.json"
            ];
        }

        return configured
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(NormalizePolicyPath)
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private bool ResolveRemoveOrphanFilesPolicy() =>
        _appSettings.UpdateInstallPolicy?.RemoveOrphanFiles ?? true;

    private static string NormalizePolicyPath(string raw) =>
        raw.Trim().Replace('/', '\\').Trim('\\');

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
            _lastDownloadedPackageSha256 = TryResolveExpectedSha256ForLocalPackage(newestZip.FullName);
            _lastDownloadedReleaseTag = null;
        }
        catch
        {
            // Ignore local package discovery failures.
        }
    }

    private string? TryResolveExpectedSha256ForLocalPackage(string packagePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(packagePath))
                return null;

            var packageName = Path.GetFileName(packagePath);
            if (string.IsNullOrWhiteSpace(packageName))
                return null;

            var updatesDir = AppPaths.GetUpdateDownloadsDirectory();
            var checksumPath = Path.Combine(updatesDir, "SHA256SUMS");
            var signaturePath = Path.Combine(updatesDir, "SHA256SUMS.sig");
            if (!File.Exists(checksumPath) || !File.Exists(signaturePath))
                return null;

            var checksumBytes = File.ReadAllBytes(checksumPath);
            var signatureBytes = File.ReadAllBytes(signaturePath);
            if (!VerifyLocalChecksumSignature(checksumBytes, signatureBytes))
                return null;

            var checksumContent = Encoding.UTF8.GetString(checksumBytes);
            return ParseSha256FromChecksumContent(checksumContent, packageName);
        }
        catch
        {
            return null;
        }
    }

    private static bool VerifyLocalChecksumSignature(byte[] checksumBytes, byte[] signatureBytes)
    {
        if (checksumBytes is null || checksumBytes.Length == 0 || signatureBytes is null || signatureBytes.Length == 0)
            return false;

        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(UpdateReleaseSigningPublicKey.Pem.Trim());
            return rsa.VerifyData(checksumBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch
        {
            return false;
        }
    }

    private static string? ParseSha256FromChecksumContent(string content, string packageFileName)
    {
        if (string.IsNullOrWhiteSpace(content) || string.IsNullOrWhiteSpace(packageFileName))
            return null;

        var lines = content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var escapedName = Regex.Escape(packageFileName);
        var pattern = $@"\b([A-Fa-f0-9]{{64}})\b(?: \s*\*?{escapedName})?$";
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
            _ => AppUiLocalization.GetString("UpdateCheckFailed")
        };
    }

    private string BuildCooldownBlockedMessage(UpdateQuotaDecision decision)
    {
        var retryAfter = decision.RetryAfter ?? TimeSpan.Zero;
        var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
        return string.Format(AppUiLocalization.GetString("UpdateQuotaCooldownBlocked"), seconds);
    }

    private string BuildDailyLimitBlockedMessage(UpdateQuotaDecision decision)
    {
        var actionText = decision.Action == UpdateQuotaAction.Download
            ? AppUiLocalization.GetString("UpdateQuotaActionDownload")
            : AppUiLocalization.GetString("UpdateQuotaActionCheck");
        return string.Format(AppUiLocalization.GetString("UpdateQuotaDailyLimitBlocked"), actionText, decision.DailyLimit);
    }

    private string BuildUpdateCheckFailedMessageWithCacheFallback()
    {
        return BuildStatusMessageWithCachedVersionHint(AppUiLocalization.GetString("UpdateCheckFailed"));
    }

    private string BuildUpdateStatusMessage(AppUpdateInfo info)
    {
        if (!string.IsNullOrWhiteSpace(info.ErrorMessage))
            return BuildStatusMessageWithCachedVersionHint(info.ErrorMessage);

        return IsUpdateAvailable ? AppUiLocalization.GetString("UpdateNewVersionAvailable") : AppUiLocalization.GetString("UpdateUpToDate");
    }

    private string BuildStatusMessageWithCachedVersionHint(string baseMessage)
    {
        var cached = _updateVersionCacheService.TryGetLatestVersion(_appSettings.GithubRepoOwner, _appSettings.GithubRepoName);
        if (cached is null)
            return baseMessage;

        var hint = string.Format(
            AppUiLocalization.GetString("UpdateCheckFailedCachedLatestHint"),
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

    private void AppendNetworkFallbackNoticeIfAny()
    {
        var notice = _updateService.ConsumeLastNetworkFallbackNotice();
        if (string.IsNullOrWhiteSpace(notice))
            return;

        if (string.IsNullOrWhiteSpace(StatusMessage))
        {
            StatusMessage = notice;
            return;
        }

        if (!StatusMessage.Contains(notice, StringComparison.Ordinal))
            StatusMessage = $"{StatusMessage} {notice}";
    }

    public void Dispose()
    {
        _downloadCts?.Dispose();
    }
}

