#nullable enable

using System.IO;
using System.Text.Json.Nodes;
using System.Windows.Media.Imaging;
using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationRoiPreviewImageProvider : IAutomationRoiPreviewImageProvider
{
    private readonly IAutomationScreenCaptureServiceResolver _captureResolver;

    public AutomationRoiPreviewImageProvider(IAutomationScreenCaptureServiceResolver captureResolver)
    {
        _captureResolver = captureResolver;
    }

    public BitmapSource? TryLoadStoredPreview(JsonObject? properties)
    {
        if (properties is null)
            return null;

        var cachePath = AutomationNodePropertyReader.ReadString(properties,
            AutomationNodePropertyKeys.CaptureRoiCachePath);
        if (!string.IsNullOrWhiteSpace(cachePath) && File.Exists(cachePath))
        {
            try
            {
                var fullPath = Path.GetFullPath(cachePath);
                var img = new BitmapImage();
                img.BeginInit();
                img.UriSource = new Uri(fullPath, UriKind.Absolute);
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.EndInit();
                if (img.CanFreeze)
                    img.Freeze();
                return img;
            }
            catch
            {
            }
        }

        return null;
    }

    public BitmapSource? TryCaptureLivePreview(JsonObject? properties)
    {
        if (!AutomationDirectScreenCapture.TryDirectCapture(_captureResolver, properties, out var direct))
            return null;

        var bmp = direct.Bitmap;
        if (bmp.CanFreeze)
            bmp.Freeze();
        return bmp;
    }
}
