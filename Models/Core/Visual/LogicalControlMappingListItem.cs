#nullable enable

using GamepadMapperGUI.Models;

namespace Gamepad_Mapping.Models.Core.Visual;

public sealed class LogicalControlMappingListItem
{
    public required MappingEntry Mapping { get; init; }

    public required string ActionSummaryLine { get; init; }

    public required string InputSummaryLine { get; init; }
}
