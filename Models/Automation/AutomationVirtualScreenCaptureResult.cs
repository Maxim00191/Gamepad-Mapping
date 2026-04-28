using System.Windows.Media.Imaging;

namespace GamepadMapperGUI.Models.Automation;

public readonly record struct AutomationVirtualScreenCaptureResult(
    BitmapSource Bitmap,
    AutomationVirtualScreenMetrics Metrics);
