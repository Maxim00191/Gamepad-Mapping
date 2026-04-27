#nullable enable

namespace GamepadMapperGUI.Services.Automation.NodeHandlers;

public sealed class PidControllerState
{
    public double PreviousError { get; set; }

    public double IntegralAccumulator { get; set; }

    public bool Initialized { get; set; }
}
