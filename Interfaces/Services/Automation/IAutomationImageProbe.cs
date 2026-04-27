using System.Windows.Media.Imaging;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Interfaces.Services.Automation;

public interface IAutomationImageProbe
{
    ValueTask<AutomationImageProbeResult> ProbeAsync(
        BitmapSource haystack,
        int haystackLeftScreenPx,
        int haystackTopScreenPx,
        BitmapSource? needle,
        AutomationImageProbeOptions options,
        AutomationVisionAlgorithmKind algorithmKind,
        CancellationToken cancellationToken);
}
