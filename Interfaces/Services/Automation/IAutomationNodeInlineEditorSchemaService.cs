using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Interfaces.Services.Automation;

public interface IAutomationNodeInlineEditorSchemaService
{
    IReadOnlyList<AutomationNodeInlineEditorDefinition> GetDefinitions(string nodeTypeId);
}
