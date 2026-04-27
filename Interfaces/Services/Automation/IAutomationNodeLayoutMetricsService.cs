using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Interfaces.Services.Automation;

public interface IAutomationNodeLayoutMetricsService
{
    AutomationNodeLayoutMetrics Build(
        IReadOnlyList<string> inputPortLabels,
        IReadOnlyList<string> outputPortLabels,
        int inlineEditorCount);
}
