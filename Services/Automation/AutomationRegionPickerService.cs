using System.Windows.Threading;
using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;
using Gamepad_Mapping.Views.Automation;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationRegionPickerService : IAutomationRegionPickerService
{
    private readonly IAutomationScreenCaptureService _capture;
    private readonly IAutomationCaptureShellHideService _shellHide;
    private readonly Dispatcher _dispatcher;

    public AutomationRegionPickerService(
        IAutomationScreenCaptureService capture,
        IAutomationCaptureShellHideService shellHide,
        Dispatcher dispatcher)
    {
        _capture = capture;
        _shellHide = shellHide;
        _dispatcher = dispatcher;
    }

    public async Task<AutomationRegionPickResult?> PickRectanglePhysicalAsync(CancellationToken cancellationToken = default)
    {
        var op = _dispatcher.InvokeAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            AutomationRegionPickResult? outcome = null;
            _shellHide.RunWhileMainWindowHidden(() =>
            {
                using var _ = AutomationDpiAwarenessScope.EnterPerMonitorAware();
                cancellationToken.ThrowIfCancellationRequested();
                var cap = _capture.CaptureVirtualScreenPhysical();
                var dlg = new AutomationRegionPickerWindow(cap.Bitmap, cap.Metrics);
                var ok = dlg.ShowDialog();
                if (ok == true &&
                    dlg.ResultRect is { } rect &&
                    !rect.IsEmpty)
                    outcome = new AutomationRegionPickResult(rect, dlg.ResultCrop);
            });

            return outcome;
        }, DispatcherPriority.Normal);

        return await op.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }
}
