using System.Threading;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Interfaces.Services.Automation;

public interface IAutomationRegionPickerService
{
    Task<AutomationRegionPickResult?> PickRectanglePhysicalAsync(CancellationToken cancellationToken = default);
}
