#nullable enable

using System.Collections.Generic;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Interfaces.Services.Automation;

public interface IAutomationRuntimeNodeHandler
{
    string NodeTypeId { get; }

    Guid? Execute(AutomationRuntimeContext context, AutomationNodeState node, IList<string> log, CancellationToken cancellationToken);
}
