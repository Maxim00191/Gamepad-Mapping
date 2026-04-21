#nullable enable

using System;
using System.Linq;
using Gamepad_Mapping;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Update;
using GamepadMapperGUI.Models;
using Gamepad_Mapping.ViewModels;

namespace GamepadMapperGUI.Services.Infrastructure;

/// <summary>
/// Centralizes startup toast logic (update success/failure, welcome) so it stays consistent
/// and matches <see cref="IUpdateNotificationService"/> acknowledgements.
/// </summary>
public sealed class LaunchInitialToastScheduler : ILaunchInitialToastScheduler
{
    private readonly IAppToastService _appToastService;
    private readonly IUpdateNotificationService _updateNotificationService;
    private readonly SettingsOrchestrator _settingsOrchestrator;

    public LaunchInitialToastScheduler(
        IAppToastService appToastService,
        IUpdateNotificationService updateNotificationService,
        SettingsOrchestrator settingsOrchestrator)
    {
        _appToastService = appToastService ?? throw new ArgumentNullException(nameof(appToastService));
        _updateNotificationService = updateNotificationService ?? throw new ArgumentNullException(nameof(updateNotificationService));
        _settingsOrchestrator = settingsOrchestrator ?? throw new ArgumentNullException(nameof(settingsOrchestrator));
    }

    /// <inheritdoc />
    public void ScheduleInitial()
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
                Title = _settingsOrchestrator.Localize("DebugCornerToast_Title"),
                Message = _settingsOrchestrator.Localize("DebugCornerToast_Message"),
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
}
