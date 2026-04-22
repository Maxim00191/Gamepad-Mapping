#nullable enable
using GamepadMapperGUI.Interfaces.Services.Editing;

namespace GamepadMapperGUI.Services.Editing;

public sealed class InMemoryEditorClipboard<T> : IEditorClipboard<T> where T : class
{
    private T? _payload;

    public bool HasContent => _payload is not null;

    public void Store(T payload) =>
        _payload = payload ?? throw new System.ArgumentNullException(nameof(payload));

    public bool TryGet(out T? payload)
    {
        payload = _payload;
        return _payload is not null;
    }

    public void Clear() => _payload = null;
}
