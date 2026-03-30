using System;
using System.Collections.Generic;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Interfaces.Core;

public interface IMappingEngine : IDisposable
{
    InputFrameProcessingResult ProcessInputFrame(InputFrame frame, IReadOnlyList<MappingEntry> mappingsSnapshot);
    void ForceReleaseAllOutputs();
    void ForceReleaseAnalogOutputs();
}
