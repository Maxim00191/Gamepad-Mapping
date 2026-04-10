using Vortice.XInput;

namespace GamepadMapperGUI.Interfaces;

public interface IXInput
{
    bool GetState(uint userIndex, out State state);
}
