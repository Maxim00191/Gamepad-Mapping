#nullable enable

using System.Collections.Generic;
using Gamepad_Mapping.Models.Core.Visual;
using GamepadMapperGUI.Models;

namespace Gamepad_Mapping.Interfaces.Services.ControllerVisual;

public interface IControllerMappingOverlayLabelComposer
{
    ControllerMappingOverlayLabelSnapshot Compose(
        string elementId,
        IReadOnlyList<MappingEntry> elementMappings,
        IEnumerable<MappingEntry> allMappings,
        string? selectedElementId,
        ControllerMappingOverlayPrimaryLabelMode primaryLabelMode,
        bool overlayShowSecondary);
}
