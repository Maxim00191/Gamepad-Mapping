using System.Collections.Generic;
using System.Windows;
using Gamepad_Mapping.Models.State;

namespace GamepadMapperGUI.Interfaces.Services.Infrastructure;

public interface IItemSelectionDialogService
{
    string? Select(
        Window? owner,
        string title,
        string searchPlaceholder,
        IReadOnlyList<SelectionDialogItem> items,
        string? initiallySelectedKey);
}
