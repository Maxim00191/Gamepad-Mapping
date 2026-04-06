using System;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Gamepad_Mapping.Utils;
using Gamepad_Mapping.ViewModels;
using Gamepad_Mapping.Views;
using GamepadMapperGUI.Interfaces.Services;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Services;

public sealed class RadialMenuHudPresenter : IRadialMenuHud
{
    private readonly Func<RadialMenuHudLabelMode> _getLabelMode;
    private readonly Func<int> _getComboHudPanelAlpha;
    private RadialMenuHudWindow? _window;
    private double _windowHudScale = double.NaN;

    public RadialMenuHudPresenter(
        Func<RadialMenuHudLabelMode> getLabelMode,
        Func<int> getComboHudPanelAlpha)
    {
        _getLabelMode = getLabelMode;
        _getComboHudPanelAlpha = getComboHudPanelAlpha;
    }

    public void ShowMenu(string title, IReadOnlyList<RadialMenuHudItem> items)
    {
        var scale = RadialHudLayout.HudScale;
        if (_window is not null &&
            (!double.IsNaN(_windowHudScale) && Math.Abs(_windowHudScale - scale) > 1e-6))
        {
            try
            {
                _window.Close();
            }
            catch
            {
                // Best-effort: recreate on next line.
            }

            _window = null;
        }

        _window ??= new RadialMenuHudWindow();
        _windowHudScale = scale;
        var n = items.Count;
        var mode = _getLabelMode();
        var vms = items.Select((i, idx) =>
            RadialMenuHudItemViewModelFactory.Create(i, idx, n, mode));
        _window.ShowMenu(title, vms, _getComboHudPanelAlpha());
    }

    public void HideMenu()
    {
        _window?.HideMenu();
    }

    public void UpdateSelection(int index)
    {
        _window?.UpdateSelection(index);
    }

    public void Dispose()
    {
        if (_window is not { } w)
            return;

        _window = null;
        _windowHudScale = double.NaN;
        try
        {
            if (Application.Current?.Dispatcher.CheckAccess() == true)
                w.Close();
            else
                Application.Current?.Dispatcher.Invoke(() => w.Close(), DispatcherPriority.Normal);
        }
        catch
        {
            // Best-effort shutdown.
        }
    }
}
