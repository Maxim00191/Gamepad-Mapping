using System.Windows.Media.Imaging;

namespace GamepadMapperGUI.Interfaces.Services.Automation;

/// <summary>
/// Captures the desktop as <see cref="BitmapSource"/> (physical pixels). Origin and size align with Win32 SM_X/Y/CX/CY VIRTUALSCREEN.
/// Automation ROI rectangles and mouse injection use this same pixel space; they are not WPF DIP units.
/// </summary>
public interface IAutomationScreenCaptureService
{
    BitmapSource CaptureVirtualScreenPhysical();

    BitmapSource CaptureRectanglePhysical(int physicalOriginX, int physicalOriginY, int widthPx, int heightPx);
}
