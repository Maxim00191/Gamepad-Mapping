#nullable enable

using HidSharp;
using GamepadMapperGUI.Interfaces.Services.Input;

namespace GamepadMapperGUI.Services.Input;

public sealed class HidSharpDualSenseHidStream(HidStream stream) : IDualSenseHidStream
{
    private readonly HidStream _stream = stream ?? throw new ArgumentNullException(nameof(stream));

    public int ReadTimeout
    {
        get => _stream.ReadTimeout;
        set => _stream.ReadTimeout = value;
    }

    public int Read(byte[] buffer, int offset, int count) => _stream.Read(buffer, offset, count);

    public void Dispose() => _stream.Dispose();
}
