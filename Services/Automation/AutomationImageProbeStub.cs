using System.Windows.Media.Imaging;
using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationImageProbeStub : IAutomationImageProbe
{
    public ValueTask<AutomationImageProbeResult> ProbeAsync(
        BitmapSource haystack,
        int haystackLeftScreenPx,
        int haystackTopScreenPx,
        BitmapSource? needle,
        AutomationImageProbeOptions options,
        AutomationVisionAlgorithmKind algorithmKind,
        CancellationToken cancellationToken)
    {
        _ = algorithmKind;
        cancellationToken.ThrowIfCancellationRequested();
        if (needle is not null)
        {
            _ = options;
        }

        var w = Math.Max(0, haystack.PixelWidth);
        var h = Math.Max(0, haystack.PixelHeight);
        if (w == 0 || h == 0)
            return ValueTask.FromResult(new AutomationImageProbeResult(false, 0, 0));

        var cx = haystackLeftScreenPx + w / 2;
        var cy = haystackTopScreenPx + h / 2;
        return ValueTask.FromResult(new AutomationImageProbeResult(true, cx, cy));
    }
}
