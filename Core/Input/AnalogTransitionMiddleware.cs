using System;
using System.Collections.Generic;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Core;

internal sealed class AnalogTransitionMiddleware : IInputFrameMiddleware
{
    private readonly AnalogMappingProcessor _analogMappingProcessor;
    private readonly Action<PointerAction, TriggerMoment> _sendPointerAction;
    private readonly Action<float, GamepadBindingType> _processNativeTriggerActions;
    private readonly Func<IReadOnlyList<MappingEntry>> _getMappingsSnapshot;

    public AnalogTransitionMiddleware(
        AnalogMappingProcessor analogMappingProcessor,
        Action<PointerAction, TriggerMoment> sendPointerAction,
        Action<float, GamepadBindingType> processNativeTriggerActions,
        Func<IReadOnlyList<MappingEntry>> getMappingsSnapshot)
    {
        _analogMappingProcessor = analogMappingProcessor;
        _sendPointerAction = sendPointerAction;
        _processNativeTriggerActions = processNativeTriggerActions;
        _getMappingsSnapshot = getMappingsSnapshot;
    }

    public void Invoke(InputFrameContext context, Action<InputFrameContext> next)
    {
        var frame = context.Frame;
        var mappings = _getMappingsSnapshot();

        // 1. Thumbsticks
        _analogMappingProcessor.ProcessThumbstick(
            GamepadBindingType.LeftThumbstick, 
            frame.LeftThumbstick, 
            mappings, 
            context.ConsumedInputs.Contains(GamepadBindingType.LeftThumbstick));

        _analogMappingProcessor.ProcessThumbstick(
            GamepadBindingType.RightThumbstick, 
            frame.RightThumbstick, 
            mappings, 
            context.ConsumedInputs.Contains(GamepadBindingType.RightThumbstick));

        // 2. Triggers (Keyboard/Pointer mappings)
        _analogMappingProcessor.ProcessTrigger(
            GamepadBindingType.LeftTrigger, 
            frame.LeftTrigger, 
            mappings, 
            _sendPointerAction);

        _analogMappingProcessor.ProcessTrigger(
            GamepadBindingType.RightTrigger, 
            frame.RightTrigger, 
            mappings, 
            _sendPointerAction);

        // 3. Native Trigger Actions (Radial Menu, Toggle, etc.)
        _processNativeTriggerActions(frame.LeftTrigger, GamepadBindingType.LeftTrigger);
        _processNativeTriggerActions(frame.RightTrigger, GamepadBindingType.RightTrigger);

        next(context);
    }
}
