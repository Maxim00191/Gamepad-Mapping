using System;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Gamepad_Mapping.ViewModels;
using Gamepad_Mapping.Views;
using GamepadMapperGUI.Interfaces.Services;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Services;

public sealed class RadialMenuHudPresenter : IRadialMenuHud
{
    private readonly Func<RadialMenuHudLabelMode> _getLabelMode;
    private RadialMenuHudWindow? _window;

    public RadialMenuHudPresenter(Func<RadialMenuHudLabelMode> getLabelMode)
    {
        _getLabelMode = getLabelMode;
    }

    public void ShowMenu(string title, IReadOnlyList<RadialMenuHudItem> items)
    {
        _window ??= new RadialMenuHudWindow();
        var n = items.Count;
        var mode = _getLabelMode();
        var vms = items.Select((i, idx) =>
            RadialMenuHudItemViewModelFactory.Create(i, idx, n, mode));
        _window.ShowMenu(title, vms);
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
