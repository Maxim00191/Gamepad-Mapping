using System;
using System.Collections.Generic;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Interfaces.Core;

public interface IMappingEngine : IDisposable
{
    /// <summary>Names from profile <c>comboLeadButtons</c>; null = infer from mappings.</summary>
    void SetComboLeadButtonsFromTemplate(IReadOnlyList<string>? comboLeadButtonNames);

    InputFrameProcessingResult ProcessInputFrame(InputFrame frame, IReadOnlyList<MappingEntry> mappingsSnapshot);
    void ForceReleaseAllOutputs();
    void ForceReleaseAnalogOutputs();

    /// <summary>
    /// Waits until all queued background outputs have been dispatched.
    /// Used primarily in tests to avoid flaky assertions.
    /// </summary>
    Task WaitForIdleAsync();
}
