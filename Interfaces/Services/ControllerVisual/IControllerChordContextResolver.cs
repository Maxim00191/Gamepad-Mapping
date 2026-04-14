#nullable enable

using System.Collections.Generic;
using GamepadMapperGUI.Models;

namespace Gamepad_Mapping.Interfaces.Services.ControllerVisual;

public interface IControllerChordContextResolver
{
    HashSet<string> GetChordParticipantElementIds(string? selectedElementId, IEnumerable<MappingEntry> mappings);

    MappingEntry? FindChordMappingBetween(
        string? selectedElementId,
        string elementId,
        IEnumerable<MappingEntry> mappings);
}
