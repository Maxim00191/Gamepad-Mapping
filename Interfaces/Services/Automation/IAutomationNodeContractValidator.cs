#nullable enable

using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Interfaces.Services.Automation;

public interface IAutomationNodeContractValidator
{
    bool TryValidate(AutomationGraphDocument document, IAutomationExecutionGraphIndex index, out string? detail);
}
