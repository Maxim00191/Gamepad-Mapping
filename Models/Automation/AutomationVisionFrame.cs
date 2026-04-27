#nullable enable

using System.Windows.Media.Imaging;

namespace GamepadMapperGUI.Models.Automation;

public sealed record AutomationVisionFrame(
    BitmapSource Image,
    BitmapSource? Needle,
    int OriginScreenX,
    int OriginScreenY,
    AutomationImageProbeOptions ProbeOptions,
    int HsvHueMin = 0,
    int HsvHueMax = 179,
    int HsvSaturationMin = 0,
    int HsvSaturationMax = 255,
    int HsvValueMin = 0,
    int HsvValueMax = 255);
