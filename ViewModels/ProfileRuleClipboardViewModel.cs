#nullable enable
using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamepadMapperGUI.Interfaces.Services.Editing;

namespace Gamepad_Mapping.ViewModels;

/// <summary>Copy/paste and undo/redo chrome for the profile template workspace (mappings, keyboard actions, radial menus).</summary>
public partial class ProfileRuleClipboardViewModel : ObservableObject
{
    private readonly MainViewModel _main;

    public ProfileRuleClipboardViewModel(MainViewModel main)
    {
        _main = main ?? throw new ArgumentNullException(nameof(main));
    }

    public void RefreshCommandStates()
    {
        CopyRuleCommand.NotifyCanExecuteChanged();
        PasteRuleCommand.NotifyCanExecuteChanged();
        SelectAllWorkspaceRulesCommand.NotifyCanExecuteChanged();
        DeleteWorkspaceRulesCommand.NotifyCanExecuteChanged();
        UndoWorkspaceEditCommand.NotifyCanExecuteChanged();
        RedoWorkspaceEditCommand.NotifyCanExecuteChanged();
    }

    private IEditorWorkspace Workspace => _main.ActiveEditorWorkspace;

    private bool CanUndoWorkspaceEdit() =>
        _main.SelectedTemplate is not null && Workspace.History.CanUndo;

    private bool CanRedoWorkspaceEdit() =>
        _main.SelectedTemplate is not null && Workspace.History.CanRedo;

    [RelayCommand(CanExecute = nameof(CanUndoWorkspaceEdit))]
    private void UndoWorkspaceEdit() => Workspace.History.Undo();

    [RelayCommand(CanExecute = nameof(CanRedoWorkspaceEdit))]
    private void RedoWorkspaceEdit() => Workspace.History.Redo();

    private bool CanCopy() => _main.SelectedTemplate is not null && Workspace.CanCopy;

    private bool CanPaste() => _main.SelectedTemplate is not null && Workspace.CanPaste;

    private bool CanSelectAll() => _main.SelectedTemplate is not null && Workspace.CanSelectAll;

    private bool CanDelete() => _main.SelectedTemplate is not null && Workspace.CanDelete;

    [RelayCommand(CanExecute = nameof(CanCopy))]
    private void CopyRule()
    {
        Workspace.Copy();
        RefreshCommandStates();
    }

    [RelayCommand(CanExecute = nameof(CanPaste))]
    private void PasteRule()
    {
        Workspace.Paste();
        RefreshCommandStates();
    }

    [RelayCommand(CanExecute = nameof(CanSelectAll))]
    private void SelectAllWorkspaceRules()
    {
        Workspace.SelectAll();
        RefreshCommandStates();
    }

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private void DeleteWorkspaceRules()
    {
        Workspace.Delete();
        RefreshCommandStates();
    }
}
