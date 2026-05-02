using System.Windows.Input;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Interfaces.Services.Automation;

public interface IAutomationProcessWindowInputDispatcher
{
    bool TryKeyDown(string processName, Key key);
    bool TryKeyUp(string processName, Key key);
    bool TryTapKey(string processName, Key key, int holdMilliseconds);
    bool TryMouseDown(string processName, string button, int screenX, int screenY);
    bool TryMouseUp(string processName, string button, int screenX, int screenY);
    bool TryMouseClick(string processName, string button, int screenX, int screenY, int holdMilliseconds);
    bool TryKeyDown(AutomationProcessWindowTarget processTarget, Key key);
    bool TryKeyUp(AutomationProcessWindowTarget processTarget, Key key);
    bool TryTapKey(AutomationProcessWindowTarget processTarget, Key key, int holdMilliseconds);
    bool TryMouseDown(AutomationProcessWindowTarget processTarget, string button, int screenX, int screenY);
    bool TryMouseUp(AutomationProcessWindowTarget processTarget, string button, int screenX, int screenY);
    bool TryMouseClick(AutomationProcessWindowTarget processTarget, string button, int screenX, int screenY, int holdMilliseconds);
}
