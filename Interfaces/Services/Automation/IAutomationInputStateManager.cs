#nullable enable

using System.Windows.Input;

namespace GamepadMapperGUI.Interfaces.Services.Automation;

public interface IAutomationInputStateManager
{
    bool IsHeld(Key key);

    bool Hold(Key key);

    bool Release(Key key);
}
