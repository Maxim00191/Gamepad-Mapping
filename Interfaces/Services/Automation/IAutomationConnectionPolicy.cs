using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Interfaces.Services.Automation;

public interface IAutomationConnectionPolicy
{
    bool ShouldReplaceIncomingConnection(AutomationPortDescriptor outputPort, AutomationPortDescriptor inputPort);
}
