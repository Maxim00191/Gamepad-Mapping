using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Windows;
using System.Windows.Media;
using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;
using GamepadMapperGUI.Utils;

namespace Gamepad_Mapping.Utils.ControllerSvg;

public static class ControllerSvgDrawingImageLoader
{
    public static DrawingImage? TryLoad(string fileName)
    {
        var path = AppPaths.GetControllerSvgPath(fileName);
        if (!File.Exists(path)) return null;

        try
        {
            using var reader = new FileSvgReader(new WpfDrawingSettings(), isEmbedded: false);
            var drawing = reader.Read(path);
            if (drawing is null) return null;
            drawing.Freeze();
            return new DrawingImage { Drawing = drawing };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SVG drawing load failed ({path}): {ex.Message}");
            return null;
        }
    }

    public static bool TryLoadAligned(
        string fileName,
        [NotNullWhen(true)] out DrawingImage? image,
        out ControllerSvgViewport viewport,
        [NotNullWhen(true)] out Transform? interactionLayerTransform)
    {
        image = null;
        interactionLayerTransform = null;
        viewport = default;

        var path = AppPaths.GetControllerSvgPath(fileName);
        if (!File.Exists(path)) return false;
        if (!ControllerSvgViewport.TryReadSvgRoot(path, out viewport)) return false;

        try
        {
            using var reader = new FileSvgReader(new WpfDrawingSettings(), isEmbedded: false);
            var drawing = reader.Read(path);
            if (drawing is null) return false;

            var bounds = drawing.Bounds;
            var intoViewport = ControllerSvgViewTransform.CreateUniformCenteredToViewport(bounds, viewport);
            var forInteraction = ControllerSvgViewTransform.CreateUniformCenteredToViewport(bounds, viewport);

            var wrapper = new DrawingGroup();
            wrapper.Children.Add(drawing);
            wrapper.Transform = intoViewport;
            wrapper.Freeze();

            image = new DrawingImage { Drawing = wrapper };
            image.Freeze();

            interactionLayerTransform = forInteraction;
            interactionLayerTransform.Freeze();
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SVG aligned drawing load failed ({path}): {ex.Message}");
            return false;
        }
    }
}
