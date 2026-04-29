#nullable enable

using System.Windows.Input;
using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation.NodeHandlers;

public sealed class KeyboardKeyNodeHandler : IAutomationRuntimeNodeHandler
{
    public string NodeTypeId => "output.keyboard_key";

    public Guid? Execute(AutomationRuntimeContext context, AutomationNodeState node, IList<string> log, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!AutomationOutputDispatchGuard.CanDispatch(context, "keyboard_key", log))
            return context.GetExecutionTarget(node.Id, "flow.out");

        var keyText = AutomationNodePropertyReader.ReadString(node.Properties, AutomationNodePropertyKeys.KeyboardKey);
        if (!AutomationKeyboardKeyParser.TryParse(keyText, out var key))
        {
            log.Add($"[keyboard_key] invalid_key key_text='{keyText}'");
            return context.GetExecutionTarget(node.Id, "flow.out");
        }

        var requestedModeId = AutomationNodePropertyReader.ReadString(node.Properties, AutomationNodePropertyKeys.InputEmulationApiId);
        var (keyboard, _) = context.ResolveInputEmulationPair(requestedModeId);
        var mode = AutomationNodePropertyReader.ReadString(node.Properties, AutomationNodePropertyKeys.KeyboardActionMode);
        var normalizedMode = NormalizeMode(mode, AutomationOutputActionModes.Tap);

        if (string.Equals(normalizedMode, AutomationOutputActionModes.HoldWhileTrue, StringComparison.Ordinal))
        {
            var isTrue = ResolveHoldCondition(context, node);
            var sent = isTrue
                ? context.InputState.Hold(key)
                : context.InputState.Release(key);
            log.Add($"[keyboard_key] action=hold_while_true key={key} condition={isTrue} sent={sent}");
        }
        else if (string.Equals(normalizedMode, AutomationOutputActionModes.Press, StringComparison.Ordinal))
        {
            keyboard.KeyDown(key);
            log.Add($"[keyboard_key] action=press key={key}");
        }
        else if (string.Equals(normalizedMode, AutomationOutputActionModes.Release, StringComparison.Ordinal))
        {
            keyboard.KeyUp(key);
            log.Add($"[keyboard_key] action=release key={key}");
        }
        else if (string.Equals(normalizedMode, AutomationOutputActionModes.Hold, StringComparison.Ordinal))
        {
            var nominalHoldMs = Math.Clamp(
                AutomationNodePropertyReader.ReadInt(node.Properties, AutomationNodePropertyKeys.KeyboardHoldMilliseconds, 200),
                1,
                context.Limits.MaxDelayMilliseconds);
            var holdMs = Math.Clamp(
                context.HumanNoise?.AdjustTapHoldMs(nominalHoldMs, 10) ?? nominalHoldMs,
                1,
                context.Limits.MaxDelayMilliseconds);
            keyboard.KeyDown(key);
            Task.Delay(holdMs, cancellationToken).GetAwaiter().GetResult();
            keyboard.KeyUp(key);
            log.Add($"[keyboard_key] action=hold key={key} hold_ms={holdMs} nominal_hold_ms={nominalHoldMs}");
        }
        else
        {
            keyboard.TapKey(key);
            log.Add($"[keyboard_key] action=tap key={key} mode={normalizedMode}");
        }

        return context.GetExecutionTarget(node.Id, "flow.out");
    }

    private static bool ResolveHoldCondition(AutomationRuntimeContext context, AutomationNodeState node)
    {
        if (context.TryResolveBooleanInput(node.Id, AutomationPortIds.Condition, out var signal))
            return signal;

        return AutomationNodePropertyReader.ReadBool(node.Properties, AutomationNodePropertyKeys.OutputHoldCondition);
    }

    private static string NormalizeMode(string? mode, string fallback)
    {
        if (string.IsNullOrWhiteSpace(mode))
            return fallback;

        var normalized = mode.Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }
}
