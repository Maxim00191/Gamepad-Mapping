#nullable enable

namespace GamepadMapperGUI.Interfaces.Services.Editing;

/// <summary>Single-slot typed clipboard for an editor workspace (in-app rule clipboard, not OS clipboard).</summary>
public interface IEditorClipboard<T> where T : class
{
    bool HasContent { get; }

    void Store(T payload);

    bool TryGet(out T? payload);

    void Clear();
}
