#nullable enable

using System.Text.Json.Nodes;
using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation.NodeHandlers;

public sealed class SetVariableNodeHandler : IAutomationRuntimeNodeHandler
{
    public string NodeTypeId => "variables.set";

    public Guid? Execute(AutomationRuntimeContext context, AutomationNodeState node, IList<string> log, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var name = AutomationNodePropertyReader.ReadString(node.Properties, AutomationNodePropertyKeys.VariableName);
        if (string.IsNullOrWhiteSpace(name))
            return context.GetExecutionTarget(node.Id, "flow.out");

        var value = ResolveValue(context, node);
        context.SetVariable(name, value);
        log.Add($"var:set:{name.Trim()}");
        return context.GetExecutionTarget(node.Id, "flow.out");
    }

    private static AutomationDataValue ResolveValue(AutomationRuntimeContext context, AutomationNodeState node)
    {
        if (context.TryResolveNumberInput(node.Id, "value.number", out var number))
            return new AutomationDataValue(AutomationPortType.Number, number);
        if (context.TryResolveBooleanInput(node.Id, "value.bool", out var boolValue))
            return new AutomationDataValue(AutomationPortType.Boolean, boolValue);

        var linkedString = context.ResolveStringInput(node.Id, "value.string");
        if (!string.IsNullOrEmpty(linkedString))
            return new AutomationDataValue(AutomationPortType.String, linkedString);

        if (node.Properties?.TryGetPropertyValue(AutomationNodePropertyKeys.VariableValue, out var valueNode) == true &&
            valueNode is not null)
        {
            if (valueNode is JsonValue boolNode && boolNode.TryGetValue<bool>(out var b))
                return new AutomationDataValue(AutomationPortType.Boolean, b);
            if (valueNode is JsonValue intNode && intNode.TryGetValue<int>(out var i))
                return new AutomationDataValue(AutomationPortType.Integer, i);
            if (valueNode is JsonValue doubleNode && doubleNode.TryGetValue<double>(out var d))
                return new AutomationDataValue(AutomationPortType.Number, d);

            return new AutomationDataValue(AutomationPortType.String, valueNode.ToString());
        }

        return AutomationDataValue.Empty;
    }
}
