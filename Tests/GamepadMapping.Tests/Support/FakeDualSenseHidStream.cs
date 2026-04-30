#nullable enable

using GamepadMapperGUI.Interfaces.Services.Input;

namespace GamepadMapping.Tests.Support;

internal sealed class FakeDualSenseHidStream(IEnumerable<byte[]> reports) : IDualSenseHidStream
{
    private readonly Queue<byte[]> _reports = new(reports);
    private int _readTimeout;

    public int ReadCount { get; private set; }
    public bool IsDisposed { get; private set; }

    public int ReadTimeout
    {
        get => _readTimeout;
        set => _readTimeout = value;
    }

    public int Read(byte[] buffer, int offset, int count)
    {
        ReadCount++;
        if (_reports.Count == 0)
            throw new TimeoutException();

        var report = _reports.Dequeue();
        var bytesToCopy = Math.Min(report.Length, count);
        Buffer.BlockCopy(report, 0, buffer, offset, bytesToCopy);
        return bytesToCopy;
    }

    public void Dispose()
    {
        IsDisposed = true;
    }
}
