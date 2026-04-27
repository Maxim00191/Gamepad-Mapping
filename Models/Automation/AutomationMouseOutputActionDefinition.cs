#nullable enable

namespace GamepadMapperGUI.Models.Automation;

public sealed record AutomationMouseOutputActionDefinition(
    string ActionId,
    string ActionMode,
    string Button,
    string LabelResourceKey);
