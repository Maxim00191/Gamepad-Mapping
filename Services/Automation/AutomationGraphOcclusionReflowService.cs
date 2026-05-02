#nullable enable

using System.Collections.Generic;
using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationGraphOcclusionReflowService : IAutomationGraphOcclusionReflowService
{
    public bool TryComputeRightShift(
        AutomationGraphNodeLayoutBounds insertedNodeBounds,
        IEnumerable<AutomationGraphNodeLayoutBounds> downstreamNodeBounds,
        double gutterLogical,
        out double shiftDeltaX)
    {
        shiftDeltaX = 0d;
        if (insertedNodeBounds.Width <= 0d || insertedNodeBounds.Height <= 0d)
            return false;

        var requiredLeft = insertedNodeBounds.Right + Math.Max(0d, gutterLogical);
        foreach (var downstream in downstreamNodeBounds)
        {
            if (downstream.NodeId == insertedNodeBounds.NodeId ||
                downstream.Width <= 0d ||
                downstream.Height <= 0d ||
                !OverlapsVertically(insertedNodeBounds, downstream) ||
                downstream.X >= requiredLeft)
            {
                continue;
            }

            shiftDeltaX = Math.Max(shiftDeltaX, requiredLeft - downstream.X);
        }

        return shiftDeltaX > 0.01d;
    }

    private static bool OverlapsVertically(
        AutomationGraphNodeLayoutBounds a,
        AutomationGraphNodeLayoutBounds b) =>
        a.Y < b.Bottom && a.Bottom > b.Y;
}
