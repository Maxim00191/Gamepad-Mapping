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
}
