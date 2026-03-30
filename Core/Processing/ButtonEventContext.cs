using System.Collections.Generic;
using GamepadMapperGUI.Models;
using Vortice.XInput;

namespace GamepadMapperGUI.Core;

internal sealed class ButtonEventContext
{
    public required GamepadButtons Button { get; init; }

    public required TriggerMoment Trigger { get; init; }

    public required IReadOnlyCollection<GamepadButtons> ActiveButtons { get; init; }

    public required IReadOnlyList<MappingEntry> MappingsSnapshot { get; init; }

    public required float LeftTriggerValue { get; init; }

    public required float RightTriggerValue { get; init; }

    public bool IsSuppressed { get; set; }

    public IReadOnlySet<DispatchedOutput>? ReleasedOutputsHandledByMappings { get; set; }

    public string ButtonName => Button.ToString();
}
