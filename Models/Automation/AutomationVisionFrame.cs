#nullable enable

using System.Windows.Media.Imaging;

namespace GamepadMapperGUI.Models.Automation;

public sealed record AutomationVisionFrame(
    BitmapSource Image,
    BitmapSource? Needle,
    int OriginScreenX,
    int OriginScreenY,
    AutomationImageProbeOptions ProbeOptions);
