using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Interfaces.Services.Automation;

public interface IAutomationGraphSerializer
{
    string Serialize(AutomationGraphDocument document);

    AutomationGraphDocument Deserialize(string json);
}
