#nullable enable

using System.Windows.Input;
using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Interfaces.Services.Input;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationInputStateManager(IKeyboardEmulator keyboard) : IAutomationInputStateManager
{
    private readonly IKeyboardEmulator _keyboard = keyboard;
    private readonly HashSet<Key> _held = [];

    public bool IsHeld(Key key) => _held.Contains(key);

    public bool Hold(Key key)
    {
        if (_held.Contains(key))
            return false;

        _keyboard.KeyDown(key);
        _held.Add(key);
        return true;
    }

    public bool Release(Key key)
    {
        if (!_held.Contains(key))
            return false;

        _keyboard.KeyUp(key);
        _held.Remove(key);
        return true;
    }
}
