using GamepadMapperGUI.Interfaces.Services.Infrastructure;

namespace GamepadMapperGUI.Services.Infrastructure;

/// <inheritdoc cref="IMainShellVisibility"/>
public sealed class MainShellVisibility : IMainShellVisibility
{
    private bool _hiddenToTray;

    public bool IsPrimaryShellHiddenToTray => _hiddenToTray;

    public event EventHandler? PrimaryShellHiddenToTrayChanged;

    public void NotifyPrimaryShellHiddenToTray()
    {
        if (_hiddenToTray)
            return;
        _hiddenToTray = true;
        PrimaryShellHiddenToTrayChanged?.Invoke(this, EventArgs.Empty);
    }

    public void NotifyPrimaryShellShownFromTray()
    {
        if (!_hiddenToTray)
            return;
        _hiddenToTray = false;
        PrimaryShellHiddenToTrayChanged?.Invoke(this, EventArgs.Empty);
    }
}
