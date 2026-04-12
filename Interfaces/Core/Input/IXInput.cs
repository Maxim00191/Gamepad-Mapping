using Vortice.XInput;

namespace GamepadMapperGUI.Interfaces.Core;

public interface IXInput
{
    bool GetState(uint userIndex, out State state);
}
