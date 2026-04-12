using GamepadMapperGUI.Models;
using GamepadMapperGUI.Interfaces.Core;
using Vortice.XInput;

namespace GamepadMapping.Tests.Mocks;

public class MockXInput : IXInput
{
    public delegate bool GetStateDelegate(uint userIndex, out State state);
    public GetStateDelegate? GetStateFunc { get; set; }

    public bool GetState(uint userIndex, out State state)
    {
        if (GetStateFunc != null)
            return GetStateFunc(userIndex, out state);
        state = new State();
        return false;
    }
}

