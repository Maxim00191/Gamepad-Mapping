using System.Windows.Media.Imaging;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Interfaces.Services.Automation;

/// <summary>
/// Captures desktop or process windows as <see cref="AutomationVirtualScreenCaptureResult"/> (physical pixels). Metrics width/height match the bitmap pixel dimensions; origin aligns with the captured rectangle in virtual-screen pixel space.
/// Automation ROI rectangles and mouse injection use this same pixel space; they are not WPF DIP units.
/// </summary>
public interface IAutomationScreenCaptureService
{
    AutomationVirtualScreenCaptureResult CaptureVirtualScreenPhysical();

    AutomationVirtualScreenCaptureResult CaptureProcessWindowPhysical(string? processName);

    BitmapSource CaptureRectanglePhysical(int physicalOriginX, int physicalOriginY, int widthPx, int heightPx);
}
