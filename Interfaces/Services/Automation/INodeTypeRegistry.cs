using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Interfaces.Services.Automation;

public interface INodeTypeRegistry
{
    IReadOnlyCollection<AutomationNodeTypeDefinition> AllDefinitions { get; }

    AutomationNodeTypeDefinition GetRequired(string nodeTypeId);

    bool TryGet(string nodeTypeId, out AutomationNodeTypeDefinition? definition);

    AutomationPortDescriptor? ResolveInputPort(string nodeTypeId, string portId);

    AutomationPortDescriptor? ResolveOutputPort(string nodeTypeId, string portId);
}
