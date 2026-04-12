using System.Collections.Generic;
using GamepadMapperGUI.Models;

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

    /// <summary>Physical hold duration for the current button when <see cref="Trigger"/> is <see cref="TriggerMoment.Released"/>.</summary>
    public long? ReleasedButtonHeldMs { get; set; }

    public IReadOnlySet<DispatchedOutput>? ReleasedOutputsHandledByMappings { get; set; }

    /// <summary>Short release after a deferred solo <see cref="TriggerMoment.Pressed"/> on a combo-lead button; terminal should skip duplicate solo Released.</summary>
    public bool DeferredSoloLeadHandledOnRelease { get; set; }

    public string ButtonName => Button.ToString();
}

