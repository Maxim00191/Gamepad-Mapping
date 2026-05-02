#nullable enable

using System.Collections.Generic;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Interfaces.Services.Automation;

public interface IAutomationGraphOcclusionReflowService
{
    bool TryComputeRightShift(
        AutomationGraphNodeLayoutBounds insertedNodeBounds,
        IEnumerable<AutomationGraphNodeLayoutBounds> downstreamNodeBounds,
        double gutterLogical,
        out double shiftDeltaX);
}
