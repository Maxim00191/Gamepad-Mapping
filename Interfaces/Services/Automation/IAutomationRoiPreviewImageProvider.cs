#nullable enable

using System.Text.Json.Nodes;
using System.Windows.Media.Imaging;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Interfaces.Services.Automation;

/// <summary>
/// Resolves ROI preview bitmaps from stored node properties (full-resolution cache file or thumbnail payload)
/// or from a live physical rectangle capture.
/// </summary>
public interface IAutomationRoiPreviewImageProvider
{
    BitmapSource? TryLoadStoredPreview(JsonObject? properties);

    BitmapSource? TryCaptureLive(AutomationPhysicalRect roi);
}
