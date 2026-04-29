#nullable enable

namespace GamepadMapperGUI.Services.Automation.NodeHandlers;

public sealed class PidControllerState
{
    public double PreviousError { get; set; }

    public double IntegralAccumulator { get; set; }

    public bool Initialized { get; set; }

    public double LastOutput { get; set; }

    public int LastInputRevision { get; set; }

    public double LastCurrent { get; set; }

    public double LastTarget { get; set; }

    public double CachedOutput { get; set; }

    public bool HasCachedOutput { get; set; }
}
