using System.Windows.Media.Imaging;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Interfaces.Services.Automation;

public interface IAutomationImageProbe
{
    AutomationImageProbeResult Probe(
        BitmapSource haystack,
        int haystackLeftScreenPx,
        int haystackTopScreenPx,
        BitmapSource? needle,
        AutomationImageProbeOptions options);
}
