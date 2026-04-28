using System.Windows.Media.Imaging;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Interfaces.Services.Automation;

/// <summary>
/// Captures the desktop as <see cref="AutomationVirtualScreenCaptureResult"/> (physical pixels). Metrics width/height match the bitmap pixel dimensions; origin aligns with Win32 SM_X/Y VIRTUALSCREEN for the capture.
/// Automation ROI rectangles and mouse injection use this same pixel space; they are not WPF DIP units.
/// </summary>
public interface IAutomationScreenCaptureService
{
    AutomationVirtualScreenCaptureResult CaptureVirtualScreenPhysical();

    BitmapSource CaptureRectanglePhysical(int physicalOriginX, int physicalOriginY, int widthPx, int heightPx);
}
