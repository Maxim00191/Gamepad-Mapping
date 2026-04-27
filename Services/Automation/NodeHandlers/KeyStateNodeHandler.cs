#nullable enable

using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation.NodeHandlers;

public sealed class KeyStateNodeHandler : IAutomationRuntimeNodeHandler
{
    public string NodeTypeId => "output.key_state";

    public Guid? Execute(AutomationRuntimeContext context, AutomationNodeState node, List<string> log, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var keyText = AutomationNodePropertyReader.ReadString(node.Properties, AutomationNodePropertyKeys.KeyboardKey);
        if (!AutomationKeyboardKeyParser.TryParse(keyText, out var key))
        {
            log.Add($"[key_state] invalid_key key_text='{keyText}'");
            return context.GetExecutionTarget(node.Id, "flow.out");
        }

        var mode = AutomationNodePropertyReader.ReadString(node.Properties, AutomationNodePropertyKeys.KeyboardActionMode)
            .Trim()
            .ToLowerInvariant();

        if (mode == "hold")
        {
            var sent = context.InputState.Hold(key);
            log.Add($"[key_state] action=hold key={key} sent={sent}");
        }
        else if (mode == "release")
        {
            var sent = context.InputState.Release(key);
            log.Add($"[key_state] action=release key={key} sent={sent}");
        }
        else
        {
            log.Add($"[key_state] action=inspect key={key} held={context.InputState.IsHeld(key)}");
        }

        return context.GetExecutionTarget(node.Id, "flow.out");
    }
}
