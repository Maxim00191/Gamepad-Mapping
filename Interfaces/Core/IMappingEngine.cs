using System;
using System.Collections.Generic;
using System.Numerics;
using GamepadMapperGUI.Models;
using Vortice.XInput;

namespace GamepadMapperGUI.Interfaces.Core;

public interface IMappingEngine : IDisposable
{
    TriggerMoment ButtonPressedTrigger { get; }
    TriggerMoment ButtonTapTrigger { get; }
    void HandleButtonMappings(
        GamepadButtons buttons,
        TriggerMoment trigger,
        IReadOnlyCollection<GamepadButtons> activeButtons,
        IReadOnlyCollection<MappingEntry> mappings);
    void HandleThumbstickMappings(GamepadBindingType sourceType, Vector2 stickValue, IReadOnlyCollection<MappingEntry> mappings);
    void ForceReleaseAllOutputs();
    void ForceReleaseAnalogOutputs();
}
