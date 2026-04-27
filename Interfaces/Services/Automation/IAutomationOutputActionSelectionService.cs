#nullable enable

using System.Collections.Generic;
using System.Windows;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Interfaces.Services.Automation;

public interface IAutomationOutputActionSelectionService
{
    string BuildKeyboardActionPickerDisplayText(string? actionId);

    string BuildMouseActionPickerDisplayText(string? actionId);

    string? PickKeyboardActionId(Window? owner, string? initiallySelectedActionId);

    string? PickMouseActionId(Window? owner, string? initiallySelectedActionId);

    bool TryResolveKeyboardAction(string? actionId, out string resolvedKeyboardKey);

    bool TryResolveMouseAction(string? actionId, out AutomationMouseOutputActionDefinition resolvedAction);
}
