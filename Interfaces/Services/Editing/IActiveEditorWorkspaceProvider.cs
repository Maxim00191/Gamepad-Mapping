#nullable enable

namespace GamepadMapperGUI.Interfaces.Services.Editing;

/// <summary>Resolves which editor workspace is active (typically from the current profile template tab).</summary>
public interface IActiveEditorWorkspaceProvider
{
    IEditorWorkspace ActiveEditorWorkspace { get; }
}
