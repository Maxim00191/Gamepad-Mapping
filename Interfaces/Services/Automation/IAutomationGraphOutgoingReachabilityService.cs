#nullable enable

using System.Collections.Generic;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Interfaces.Services.Automation;

public interface IAutomationGraphOutgoingReachabilityService
{
    void CollectReachableTargetNodeIds(
        AutomationGraphDocument document,
        Guid originNodeId,
        HashSet<Guid> destination);
}
