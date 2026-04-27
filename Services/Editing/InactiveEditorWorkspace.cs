#nullable enable
using System;
using GamepadMapperGUI.Interfaces.Services.Editing;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Services.Editing;

/// <summary>Placeholder workspace when no rule-editing surface is active (e.g. Community tab).</summary>
public sealed class InactiveEditorWorkspace : IEditorWorkspace
{
    public static InactiveEditorWorkspace Instance { get; } = new();

    private InactiveEditorWorkspace()
    {
    }

    public EditorWorkspaceKind Kind => EditorWorkspaceKind.None;

    public IEditorHistory History => EmptyEditorHistory.Instance;

    public bool CanCopy => false;

    public bool CanPaste => false;

    public bool CanDelete => false;

    public bool CanSelectAll => false;

    public event EventHandler? StateChanged
    {
        add { }
        remove { }
    }

    public void Copy()
    {
    }

    public void Paste()
    {
    }

    public void Delete()
    {
    }

    public void SelectAll()
    {
    }

    public void ClearClipboard()
    {
    }

    public void Reload(GameProfileTemplate? template)
    {
    }
}
