namespace GamepadMapperGUI.Interfaces.Services.Infrastructure;

/// <summary>
/// Tracks whether the primary application shell (main window) is visible or intentionally hidden to the tray.
/// Mapping, gamepad polling, and overlay HUD windows are unaffected; subscribers use this to throttle non-essential
/// main-window chrome (status line, monitor panel refresh, marshaled diagnostics).
/// </summary>
public interface IMainShellVisibility
{
    /// <summary>True when the main window is hidden and the app keeps running from the notification area.</summary>
    bool IsPrimaryShellHiddenToTray { get; }

    event EventHandler? PrimaryShellHiddenToTrayChanged;

    /// <summary>Called when the main window is hidden but the process stays alive (notification icon).</summary>
    void NotifyPrimaryShellHiddenToTray();

    /// <summary>Called when the main window is shown again from the tray or otherwise becomes interactive.</summary>
    void NotifyPrimaryShellShownFromTray();
}
