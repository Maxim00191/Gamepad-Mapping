using System.Windows.Threading;
using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;
using Gamepad_Mapping.Views.Automation;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationRegionPickerService : IAutomationRegionPickerService
{
    private readonly IAutomationScreenCaptureService _capture;
    private readonly Dispatcher _dispatcher;

    public AutomationRegionPickerService(IAutomationScreenCaptureService capture, Dispatcher dispatcher)
    {
        _capture = capture;
        _dispatcher = dispatcher;
    }

    public async Task<AutomationPhysicalRect?> PickRectanglePhysicalAsync(CancellationToken cancellationToken = default)
    {
        var op = _dispatcher.InvokeAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var bmp = _capture.CaptureVirtualScreenPhysical();
            var dlg = new AutomationRegionPickerWindow(bmp);
            var ok = dlg.ShowDialog();
            return ok == true ? dlg.ResultRect : null;
        }, DispatcherPriority.Normal);

        return await op.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }
}
