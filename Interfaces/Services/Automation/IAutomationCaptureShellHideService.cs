namespace GamepadMapperGUI.Interfaces.Services.Automation;

/// <summary>
/// Temporarily hides the primary application window so desktop capture (e.g. automation region pick)
/// does not include the shell. Restores visibility afterward.
/// </summary>
public interface IAutomationCaptureShellHideService
{
    /// <summary>
    /// Runs <paramref name="action"/> on the UI thread after hiding the main window (when visible).
    /// Must be invoked from the dispatcher thread used by <see cref="System.Windows.Application"/>.
    /// </summary>
    void RunWhileMainWindowHidden(Action action);
}
