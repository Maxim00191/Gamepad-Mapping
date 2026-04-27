using System;
using GamepadMapperGUI.Models;

namespace Gamepad_Mapping.ViewModels.Strategies;

/// <summary>
/// Factory implementation for creating action editor strategies.
/// </summary>
public class ActionEditorFactory : IActionEditorFactory
{
    private readonly MainViewModel _mainViewModel;

    public ActionEditorFactory(MainViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
    }

    public ActionEditorViewModelBase Create(MappingActionType actionType)
    {
        return actionType switch
        {
            MappingActionType.Keyboard => new KeyboardActionEditorViewModel(
                _mainViewModel.KeyboardCaptureService,
                _mainViewModel.KeyboardActions,
                _mainViewModel.ItemSelectionDialogService,
                _mainViewModel.KeyboardActionSelectionBuilder),
            MappingActionType.ItemCycle => new ItemCycleActionEditorViewModel(_mainViewModel.KeyboardCaptureService),
            MappingActionType.TemplateToggle => new TemplateToggleActionEditorViewModel(_mainViewModel.GetProfileService(), _mainViewModel.SelectedTemplate?.StorageKey),
            MappingActionType.RadialMenu => new RadialMenuActionEditorViewModel(),
            _ => throw new ArgumentException($"Unsupported action type: {actionType}", nameof(actionType))
        };
    }

    public ActionEditorViewModelBase CreateForMapping(MappingEntry mapping)
    {
        var strategy = Create(mapping.ActionType);
        strategy.SyncFrom(mapping);
        return strategy;
    }
}
