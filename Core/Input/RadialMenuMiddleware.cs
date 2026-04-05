using System;
using System.Linq;
using System.Numerics;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Core;

internal sealed class RadialMenuMiddleware(
    IRadialMenuController controller,
    Func<float> getEngagementThreshold,
    Func<RadialMenuConfirmMode> getConfirmMode) : IInputFrameMiddleware
{
    private string? _lastRadialId;

    public void Invoke(InputFrameContext context, Action<InputFrameContext> next)
    {
        var activeRadial = controller.ActiveRadial;
        if (activeRadial == null)
        {
            _lastRadialId = null;
            next(context);
            return;
        }

        // Track session transitions to reset internal state if a new radial opens
        if (activeRadial.Id != _lastRadialId)
        {
            _lastRadialId = activeRadial.Id;
        }

        var frame = context.Frame;
        var stickType = activeRadial.Joystick == "LeftStick"
            ? GamepadBindingType.LeftThumbstick
            : GamepadBindingType.RightThumbstick;

        var stickValue = stickType == GamepadBindingType.LeftThumbstick
            ? frame.LeftThumbstick
            : frame.RightThumbstick;

        // Update selection and check if the stick is actually being used for the menu
        controller.UpdateSelection(stickValue, getEngagementThreshold(), getConfirmMode());

        if (stickValue.Length() >= getEngagementThreshold())
        {
            context.ConsumedInputs.Add(stickType);
        }

        next(context);
    }
}
