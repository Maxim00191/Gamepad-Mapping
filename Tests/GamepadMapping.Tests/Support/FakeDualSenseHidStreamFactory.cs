#nullable enable

using GamepadMapperGUI.Interfaces.Services.Input;

namespace GamepadMapping.Tests.Support;

internal sealed class FakeDualSenseHidStreamFactory(
    IDualSenseHidStream? stream,
    int maxInputReportLength = 64,
    bool opens = true) : IDualSenseHidStreamFactory
{
    public int OpenAttempts { get; private set; }

    public bool TryOpen(out IDualSenseHidStream? openedStream, out int openedMaxInputReportLength)
    {
        OpenAttempts++;
        openedStream = opens ? stream : null;
        openedMaxInputReportLength = opens ? maxInputReportLength : 0;
        return opens && stream is not null;
    }
}
