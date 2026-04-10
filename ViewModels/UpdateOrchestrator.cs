using System;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Storage;
using GamepadMapperGUI.Interfaces.Services.Update;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Interfaces.Services.Radial;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Services.Infrastructure;
using GamepadMapperGUI.Services.Update;

namespace Gamepad_Mapping.ViewModels;

/// <summary>
/// Orchestrates application updates and toast notifications.
/// Merges UpdateService, ToastService, and notification lifecycle logic.
/// </summary>
public partial class UpdateOrchestrator : ObservableObject, IDisposable
{
    private readonly IUpdateService _updateService;
    private readonly IUpdateNotificationService _updateNotificationService;
    private readonly IAppToastService _appToastService;
    private readonly SettingsOrchestrator _settingsOrchestrator;
    private readonly AppToastViewModel _toastHost;

    public UpdateViewModel UpdatePanel { get; }
    public AppToastViewModel ToastHost => _toastHost;

    public UpdateOrchestrator(
        IUpdateService updateService,
        IUpdateNotificationService updateNotificationService,
        IAppToastService appToastService,
        SettingsOrchestrator settingsOrchestrator,
        ISettingsService settingsService,
        AppSettings appSettings,
        ILocalFileService localFileService,
        IUpdateInstallerService updateInstallerService,
        IUpdateQuotaService updateQuotaService,
        IUpdateVersionCacheService updateVersionCacheService)
    {
        _updateService = updateService;
        _updateNotificationService = updateNotificationService;
        _appToastService = appToastService;
        _settingsOrchestrator = settingsOrchestrator;
        _toastHost = new AppToastViewModel(_appToastService);
        
        UpdatePanel = new UpdateViewModel(
            _updateService,
            settingsService,
            appSettings,
            localFileService,
            updateInstallerService,
            updateQuotaService,
            updateVersionCacheService);
    }

    public void ScheduleInitialToasts()
    {
#if DEBUG
        if (TryShowDebugCornerToast())
            return;
#endif

        var launchArgs = App.LaunchUpdateSuccessArgs;
        if (launchArgs is not null)
        {
            _appToastService.Show(new AppToastRequest
            {
                Title = _settingsOrchestrator.Localize("UpdateSuccessToastTitle"),
                Message = _settingsOrchestrator.FormatUpdateSuccessMessage(launchArgs.Value.ReleaseTag),
                AutoHideSeconds = null,
                OnClosed = () => _updateNotificationService.AcknowledgeSuccess(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                InvokeOnClosedWhenExitingApplication = true
            });
            // Clear the argument so it doesn't show again on subsequent launches from e.g. a shortcut
            App.LaunchUpdateSuccessArgs = null;
            return;
        }

        var pendingUpdate = _updateNotificationService.TryGetPendingSuccessToast();
        if (pendingUpdate is not null)
        {
            var p = pendingUpdate.Value;
            _appToastService.Show(new AppToastRequest
            {
                Title = _settingsOrchestrator.Localize("UpdateSuccessToastTitle"),
                Message = _settingsOrchestrator.FormatUpdateSuccessMessage(p.ReleaseTag),
                AutoHideSeconds = null,
                OnClosed = () => _updateNotificationService.AcknowledgeSuccess(p.UpdatedAtUnixSeconds),
                InvokeOnClosedWhenExitingApplication = true
            });
            return;
        }

        var pendingFailure = _updateNotificationService.TryGetPendingFailureToast();
        if (pendingFailure is not null)
        {
            var p = pendingFailure.Value;
            _appToastService.Show(new AppToastRequest
            {
                Title = _settingsOrchestrator.Localize("UpdateFailedToastTitle"),
                Message = p.ErrorMessage ?? _settingsOrchestrator.Localize("UpdateInstallLaunchFailed"),
                AutoHideSeconds = null,
                OnClosed = () => _updateNotificationService.AcknowledgeFailure(),
                InvokeOnClosedWhenExitingApplication = true
            });
            return;
        }

        if (!_settingsOrchestrator.Settings.HasSeenWelcomeToast)
        {
            _appToastService.Show(new AppToastRequest
            {
                Title = _settingsOrchestrator.Localize("WelcomeToastTitle"),
                Message = _settingsOrchestrator.Localize("WelcomeToastMessage"),
                AutoHideSeconds = null,
                OnClosed = () =>
                {
                    _settingsOrchestrator.Settings.HasSeenWelcomeToast = true;
                    _settingsOrchestrator.SaveSettings();
                },
                InvokeOnClosedWhenExitingApplication = false
            });
        }
    }

#if DEBUG
    private bool TryShowDebugCornerToast()
    {
        var args = Environment.GetCommandLineArgs();
        if (args.Any(a => string.Equals(a, "--debug-toast", StringComparison.OrdinalIgnoreCase)))
        {
            _appToastService.Show(new AppToastRequest
            {
                Title = "Debug: corner toast",
                Message = "Launched with --debug-toast. Dismiss or wait for auto-hide; no update or welcome flow runs.",
                AutoHideSeconds = null,
                InvokeOnClosedWhenExitingApplication = false
            });
            return true;
        }

        if (args.Any(a => string.Equals(a, "--debug-update-success", StringComparison.OrdinalIgnoreCase)))
        {
            _appToastService.Show(new AppToastRequest
            {
                Title = _settingsOrchestrator.Localize("UpdateSuccessToastTitle"),
                Message = _settingsOrchestrator.FormatUpdateSuccessMessage("vDebugSuccess"),
                AutoHideSeconds = null,
                InvokeOnClosedWhenExitingApplication = false
            });
            return true;
        }

        if (args.Any(a => string.Equals(a, "--debug-update-failed", StringComparison.OrdinalIgnoreCase)))
        {
            _appToastService.Show(new AppToastRequest
            {
                Title = _settingsOrchestrator.Localize("UpdateFailedToastTitle"),
                Message = _settingsOrchestrator.Localize("UpdateFailedToastMessage"),
                AutoHideSeconds = null,
                InvokeOnClosedWhenExitingApplication = false
            });
            return true;
        }

        return false;
    }
#endif

    public void NotifyApplicationExiting()
    {
        _appToastService.NotifyApplicationExiting();
    }

    public void Dispose()
    {
        _toastHost.Dispose();
    }
}

