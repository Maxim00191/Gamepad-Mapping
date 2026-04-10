using GamepadMapperGUI.Services.Infrastructure;
using GamepadMapperGUI.Services.Storage;
using GamepadMapperGUI.Services.Update;
using GamepadMapperGUI.Services.Input;
using GamepadMapperGUI.Services.Radial;
using GamepadMapperGUI.Interfaces.Core;
using Vortice.XInput;

namespace GamepadMapperGUI.Services.Input;

public class XInputService : IXInput
{
    public bool GetState(uint userIndex, out State state)
    {
        return XInput.GetState(userIndex, out state);
    }
}

