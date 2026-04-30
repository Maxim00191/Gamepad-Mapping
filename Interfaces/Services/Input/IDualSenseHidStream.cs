#nullable enable

namespace GamepadMapperGUI.Interfaces.Services.Input;

public interface IDualSenseHidStream : IDisposable
{
    int ReadTimeout { get; set; }

    int Read(byte[] buffer, int offset, int count);
}
