#nullable enable

using System.Windows;

namespace GamepadMapperGUI.Interfaces.Services.Automation;

public interface IAutomationVisionAlgorithmSelectionService
{
    string BuildFindImageAlgorithmPickerDisplayText(string? algorithmId);

    string? PickFindImageAlgorithm(Window? owner, string? initiallySelectedAlgorithmId);
}
