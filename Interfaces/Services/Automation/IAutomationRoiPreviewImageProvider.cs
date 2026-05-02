#nullable enable

using System.Text.Json.Nodes;
using System.Windows.Media.Imaging;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Interfaces.Services.Automation;

/// <summary>
/// Resolves capture preview bitmaps from stored node properties (ROI cache file or thumbnail)
/// or from a live capture that matches <c>perception.capture_screen</c> execution (screen, process window, or ROI).
/// </summary>
public interface IAutomationRoiPreviewImageProvider
{
    BitmapSource? TryLoadStoredPreview(JsonObject? properties);

    BitmapSource? TryCaptureLivePreview(JsonObject? properties);
}
