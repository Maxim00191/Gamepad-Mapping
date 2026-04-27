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
            log.Add($"[keyboard_key] invalid_key key_text='{keyText}'");
            return context.GetExecutionTarget(node.Id, "flow.out");
        }

        var mode = AutomationNodePropertyReader.ReadString(node.Properties, AutomationNodePropertyKeys.KeyboardActionMode);
        var normalizedMode = string.IsNullOrWhiteSpace(mode) ? "tap" : mode.Trim().ToLowerInvariant();
        if (string.Equals(mode, "press", StringComparison.OrdinalIgnoreCase))
        {
            context.Keyboard.KeyDown(key);
            log.Add($"[keyboard_key] action=press key={key}");
        }
        else if (string.Equals(mode, "release", StringComparison.OrdinalIgnoreCase))
        {
            context.Keyboard.KeyUp(key);
            log.Add($"[keyboard_key] action=release key={key}");
        }
        else if (string.Equals(mode, "hold", StringComparison.OrdinalIgnoreCase))
        {
            var nominalHoldMs = Math.Clamp(
                AutomationNodePropertyReader.ReadInt(node.Properties, AutomationNodePropertyKeys.KeyboardHoldMilliseconds, 200),
                1,
                context.Limits.MaxDelayMilliseconds);
            var holdMs = Math.Clamp(
                context.HumanNoise?.AdjustTapHoldMs(nominalHoldMs, 10) ?? nominalHoldMs,
                1,
                context.Limits.MaxDelayMilliseconds);
            context.InputState.Hold(key);
            Task.Delay(holdMs, cancellationToken).GetAwaiter().GetResult();
            context.InputState.Release(key);
            log.Add($"[keyboard_key] action=hold key={key} hold_ms={holdMs} nominal_hold_ms={nominalHoldMs}");
        }
        else
        {
            context.Keyboard.TapKey(key);
            log.Add($"[keyboard_key] action=tap key={key} requested_mode={normalizedMode}");
        }

        return context.GetExecutionTarget(node.Id, "flow.out");
    }
}
