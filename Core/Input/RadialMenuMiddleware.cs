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
        var stick = activeRadial.Joystick == "LeftStick"
            ? frame.LeftThumbstick
            : frame.RightThumbstick;

        controller.UpdateSelection(stick, getEngagementThreshold(), getConfirmMode());

                // Mark the stick as consumed
                context.ConsumedInputs.Add(activeRadial.Joystick == "LeftStick" 
                    ? GamepadBindingType.LeftThumbstick 
                    : GamepadBindingType.RightThumbstick);

                next(context);
    }
}
