#nullable enable
using System;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Interfaces.Services.Editing;

/// <summary>Non-generic contract for the active profile rule editing surface (copy/paste, undo, etc.).</summary>
public interface IEditorWorkspace
{
    EditorWorkspaceKind Kind { get; }

    IEditorHistory History { get; }

    bool CanCopy { get; }

    bool CanPaste { get; }

    bool CanDelete { get; }

    bool CanSelectAll { get; }

    void Copy();

    void Paste();

    void Delete();

    void SelectAll();

    void ClearClipboard();

    void Reload(GameProfileTemplate? template);

    event EventHandler? StateChanged;
}

/// <summary>Typed editor workspace exposing selection for bindings and services.</summary>
public interface IEditorWorkspace<TItem> : IEditorWorkspace where TItem : class
{
    ISelectionService<TItem> Selection { get; }

    IEditorClipboard<string> Clipboard { get; }
}
