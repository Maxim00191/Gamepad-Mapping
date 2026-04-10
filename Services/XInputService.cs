using GamepadMapperGUI.Interfaces;
using Vortice.XInput;

namespace GamepadMapperGUI.Services;

public class XInputService : IXInput
{
    public bool GetState(uint userIndex, out State state)
    {
        return XInput.GetState(userIndex, out state);
    }
}
