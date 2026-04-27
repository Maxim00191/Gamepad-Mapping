using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationConnectionPolicy : IAutomationConnectionPolicy
{
    public bool ShouldReplaceIncomingConnection(AutomationPortDescriptor outputPort, AutomationPortDescriptor inputPort)
    {
        if (outputPort.IsOutput == false || inputPort.IsOutput)
            return false;

        return outputPort.FlowKind == inputPort.FlowKind;
    }
}
