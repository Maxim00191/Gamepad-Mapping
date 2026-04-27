using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Interfaces.Services.Automation;

public interface IAutomationExecutionServicesFactory
{
    AutomationExecutionServices Create(
        IKeyboardEmulator keyboard,
        IMouseEmulator mouse,
        IHumanInputNoiseController? humanNoise = null,
        IAutomationNodeInputModeResolver? inputModeResolver = null);
}
