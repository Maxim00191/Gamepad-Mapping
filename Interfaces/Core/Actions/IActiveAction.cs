using GamepadMapperGUI.Models;
using GamepadMapperGUI.Core;

namespace GamepadMapperGUI.Interfaces.Core;

internal interface IActiveAction
{
    /// <summary>Unique identifier for the action instance.</summary>
    string Id { get; }

    /// <summary>Called when a button is released to check if this action should terminate.</summary>
    void HandleButtonReleased(GamepadButtons button);

    /// <summary>Forcefully terminates the action.</summary>
    void ForceCancel();
}

