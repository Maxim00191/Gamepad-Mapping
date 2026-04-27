using GamepadMapperGUI.Interfaces.Services.Input;

namespace GamepadMapperGUI.Interfaces.Services.Automation;

public interface IAutomationNodeInputModeResolver
{
    (IKeyboardEmulator Keyboard, IMouseEmulator Mouse) Resolve(string? requestedModeId);
}
