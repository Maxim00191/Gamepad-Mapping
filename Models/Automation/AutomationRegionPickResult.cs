#nullable enable

using System.Windows.Media.Imaging;

namespace GamepadMapperGUI.Models.Automation;

/// <summary>
/// Outcome of an interactive physical-pixel region pick over a frozen virtual-screen bitmap.
/// </summary>
public sealed record AutomationRegionPickResult(
    AutomationPhysicalRect Rect,
    BitmapSource? CroppedPhysicalBitmap);
