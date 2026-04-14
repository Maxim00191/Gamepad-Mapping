using System.Diagnostics.CodeAnalysis;
using GamepadMapperGUI.Models.ControllerVisual;
using Gamepad_Mapping.Utils.ControllerVisual;

namespace Gamepad_Mapping.Interfaces.Services;

public interface IControllerVisualLoader
{
    bool TryLoad(
        ControllerVisualLayoutDescriptor descriptor,
        [NotNullWhen(true)] out ControllerSvgAlignedLoadResult? result);
}
