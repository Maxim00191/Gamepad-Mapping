#nullable enable

using System.Collections.Generic;
using GamepadMapperGUI.Models;

namespace Gamepad_Mapping.Interfaces.Services.ControllerVisual;

public interface IMappingsForLogicalControlQuery
{
    IReadOnlyList<MappingEntry> GetMappingsForLogicalControl(string elementId, IEnumerable<MappingEntry> mappings);

    bool MappingInvolvesLogicalControl(MappingEntry mapping, string elementId);

    string? ResolvePrimaryLogicalControlIdForMapping(MappingEntry mapping);
}
