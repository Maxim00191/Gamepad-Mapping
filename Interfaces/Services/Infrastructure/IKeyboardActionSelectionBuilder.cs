using System.Collections.Generic;
using Gamepad_Mapping.Models.State;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Interfaces.Services.Infrastructure;

public interface IKeyboardActionSelectionBuilder
{
    IReadOnlyList<SelectionDialogItem> BuildSelectionItems(IEnumerable<KeyboardActionDefinition> keyboardActions);

    string BuildSelectedActionDisplayText(string? actionId, IEnumerable<KeyboardActionDefinition> keyboardActions);

    string BuildSelectedHoldActionDisplayText(
        string? holdActionId,
        string? holdKeyboardKey,
        IEnumerable<KeyboardActionDefinition> keyboardActions);
}
