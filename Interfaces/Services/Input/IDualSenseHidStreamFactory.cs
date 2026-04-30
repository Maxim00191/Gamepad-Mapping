#nullable enable

namespace GamepadMapperGUI.Interfaces.Services.Input;

public interface IDualSenseHidStreamFactory
{
    bool TryOpen(out IDualSenseHidStream? stream, out int maxInputReportLength);
}
