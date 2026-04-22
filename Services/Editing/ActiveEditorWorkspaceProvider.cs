#nullable enable
using System;
using System.Collections.Generic;
using GamepadMapperGUI.Interfaces.Services.Editing;
using Gamepad_Mapping.ViewModels;

namespace GamepadMapperGUI.Services.Editing;

public sealed class ActiveEditorWorkspaceProvider : IActiveEditorWorkspaceProvider
{
    private static readonly IReadOnlyDictionary<int, EditorWorkspaceKind> TabKindMap =
        new Dictionary<int, EditorWorkspaceKind>
        {
            [(int)MainViewModel.MainProfileWorkspaceTab.Mappings] = EditorWorkspaceKind.Mappings,
            [(int)MainViewModel.MainProfileWorkspaceTab.VisualEditor] = EditorWorkspaceKind.Mappings,
            [(int)MainViewModel.MainProfileWorkspaceTab.KeyboardActions] = EditorWorkspaceKind.KeyboardActions,
            [(int)MainViewModel.MainProfileWorkspaceTab.RadialMenus] = EditorWorkspaceKind.RadialMenus
        };

    private readonly Func<int> _getTabIndex;
    private readonly IReadOnlyDictionary<EditorWorkspaceKind, IEditorWorkspace> _workspaces;

    public ActiveEditorWorkspaceProvider(
        Func<int> getTabIndex,
        IReadOnlyDictionary<EditorWorkspaceKind, IEditorWorkspace> workspaces)
    {
        _getTabIndex = getTabIndex ?? throw new ArgumentNullException(nameof(getTabIndex));
        _workspaces = workspaces ?? throw new ArgumentNullException(nameof(workspaces));
    }

    public IEditorWorkspace ActiveEditorWorkspace
    {
        get
        {
            if (!TabKindMap.TryGetValue(_getTabIndex(), out var kind))
                return InactiveEditorWorkspace.Instance;

            return _workspaces.TryGetValue(kind, out var workspace)
                ? workspace
                : InactiveEditorWorkspace.Instance;
        }
    }
}
