#nullable enable

using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation.NodeHandlers;

public sealed class KeyboardKeyNodeHandler : IAutomationRuntimeNodeHandler
{
    public string NodeTypeId => "output.keyboard_key";

    public Guid? Execute(AutomationRuntimeContext context, AutomationNodeState node, List<string> log, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var keyText = AutomationNodePropertyReader.ReadString(node.Properties, AutomationNodePropertyKeys.KeyboardKey);
        if (!AutomationKeyboardKeyParser.TryParse(keyText, out var key))
        {
            log.Add("keyboard:bad_key");
            return context.GetExecutionTarget(node.Id, "flow.out");
        }

        var mode = AutomationNodePropertyReader.ReadString(node.Properties, AutomationNodePropertyKeys.KeyboardActionMode);
        if (string.Equals(mode, "press", StringComparison.OrdinalIgnoreCase))
        {
            context.Keyboard.KeyDown(key);
        }
        else if (string.Equals(mode, "release", StringComparison.OrdinalIgnoreCase))
        {
            context.Keyboard.KeyUp(key);
        }
        else if (string.Equals(mode, "hold", StringComparison.OrdinalIgnoreCase))
        {
            var holdMs = Math.Clamp(
                AutomationNodePropertyReader.ReadInt(node.Properties, AutomationNodePropertyKeys.KeyboardHoldMilliseconds, 200),
                1,
                context.Limits.MaxDelayMilliseconds);
            context.Keyboard.KeyDown(key);
            Task.Delay(holdMs, cancellationToken).GetAwaiter().GetResult();
            context.Keyboard.KeyUp(key);
        }
        else
        {
            context.Keyboard.TapKey(key);
        }

        return context.GetExecutionTarget(node.Id, "flow.out");
    }
}
