#nullable enable

using System.Threading;
using System.Windows;
using GamepadMapperGUI.Interfaces.Services.Automation;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationCaptureShellHideService : IAutomationCaptureShellHideService
{
    private const int CompositeSettleDelayMs = 75;

    public void RunWhileMainWindowHidden(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var w = Application.Current?.MainWindow;
        var hid = false;
        try
        {
            if (w is not null && w.IsVisible)
            {
                w.Hide();
                hid = true;
                Thread.Sleep(CompositeSettleDelayMs);
            }

            action();
        }
        finally
        {
            if (hid && w is not null)
            {
                w.Show();
                if (w.WindowState == WindowState.Minimized)
                    w.WindowState = WindowState.Normal;
                w.Activate();
            }
        }
    }
}
