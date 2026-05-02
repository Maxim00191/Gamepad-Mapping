#nullable enable

using System.Windows;

namespace GamepadMapperGUI.Interfaces.Services.Automation;

public interface IAutomationInputModeSelectionService
{
    string BuildInputModePickerDisplayText(string? modeId);

    string? PickInputModeId(Window? owner, string? initiallySelectedModeId);
}
