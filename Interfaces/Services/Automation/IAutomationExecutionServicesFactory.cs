using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Models.Automation;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;

namespace GamepadMapperGUI.Interfaces.Services.Automation;

public interface IAutomationExecutionServicesFactory
{
    AutomationExecutionServices Create(
        IKeyboardEmulator keyboard,
        IMouseEmulator mouse,
        IHumanInputNoiseController? humanNoise = null,
        IAutomationNodeInputModeResolver? inputModeResolver = null,
        IProcessTargetService? processTargetService = null);
}
