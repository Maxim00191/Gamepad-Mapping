using System.Collections.Generic;
using System.Windows;
using Gamepad_Mapping.Models.State;
using Gamepad_Mapping.ViewModels;
using Gamepad_Mapping.Views;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;

namespace GamepadMapperGUI.Services.Infrastructure;

public sealed class ItemSelectionDialogService : IItemSelectionDialogService
{
    public string? Select(
        Window? owner,
        string title,
        string searchPlaceholder,
        IReadOnlyList<SelectionDialogItem> items,
        string? initiallySelectedKey)
    {
        var vm = new SelectionDialogViewModel(title, searchPlaceholder, items, initiallySelectedKey);
        var dialog = new SelectionDialogWindow
        {
            Owner = owner,
            DataContext = vm
        };

        return dialog.ShowDialog() == true
            ? vm.SelectedItem?.Key
            : null;
    }
}
