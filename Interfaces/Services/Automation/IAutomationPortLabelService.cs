#nullable enable

using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Interfaces.Services.Automation;

public interface IAutomationPortLabelService
{
    string ResolveDisplayNameResourceKey(string portId, bool isOutputPort, AutomationPortFlowKind flowKind);
}
