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
    private RadialMenuHudWindow? _window;

    public void ShowMenu(string title, IReadOnlyList<RadialMenuHudItem> items)
    {
        _window ??= new RadialMenuHudWindow();
        var n = items.Count;
        var vms = items.Select((i, idx) => new RadialMenuItemViewModel
        {
            ActionId = i.ActionId,
            DisplayName = i.DisplayName,
            Icon = i.Icon,
            SegmentIndex = idx,
            SegmentCount = n
        });
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
