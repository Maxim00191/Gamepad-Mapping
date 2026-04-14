using System.Diagnostics.CodeAnalysis;
using Gamepad_Mapping.Interfaces.Services.ControllerVisual;
using Gamepad_Mapping.Utils.ControllerVisual;
using GamepadMapperGUI.Models.ControllerVisual;

namespace Gamepad_Mapping.Services.ControllerVisual;

public sealed class ControllerVisualLoader : IControllerVisualLoader
{
    public bool TryLoad(
        ControllerVisualLayoutDescriptor descriptor,
        [NotNullWhen(true)] out ControllerSvgAlignedLoadResult? result)
    {
        result = null;
        if (!ControllerSvgDrawingImageLoader.TryLoadAligned(
                descriptor.SvgFileName,
                out var image,
                out var viewport,
                out var transform,
                out var root))
            return false;

        result = new ControllerSvgAlignedLoadResult(image, viewport, transform, root);
        return true;
    }
}
