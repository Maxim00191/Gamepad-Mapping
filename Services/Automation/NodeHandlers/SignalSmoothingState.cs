#nullable enable

namespace GamepadMapperGUI.Services.Automation.NodeHandlers;

public sealed class SignalSmoothingState
{
    public bool Initialized { get; set; }

    public double Value { get; set; }
}
