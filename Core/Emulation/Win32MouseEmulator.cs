using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Gamepad_Mapping;
using GamepadMapperGUI.Interfaces.Services;
using GamepadMapperGUI.Services;

namespace GamepadMapperGUI.Core;

/// <summary>Mouse output via Win32 <c>SendInput</c> (<see cref="ISendInputChannel"/>).</summary>
public sealed class Win32MouseEmulator : IMouseEmulator
{
    private readonly ISendInputChannel _sendChannel;

    /// <summary>Brief down-hold before up, aligned with <see cref="Win32KeyboardEmulator"/> tap timing.</summary>
    private const int ClickHoldMs = 30;

    private const uint InputMouse = 0;
    private const uint MouseeventfLeftdown = 0x0002;
    private const uint MouseeventfLeftup = 0x0004;
    private const uint MouseeventfRightdown = 0x0008;
    private const uint MouseeventfRightup = 0x0010;
    private const uint MouseeventfMiddledown = 0x0020;
    private const uint MouseeventfMiddleup = 0x0040;
    private const uint MouseeventfXdown = 0x0080;
    private const uint MouseeventfXup = 0x0100;
    private const uint MouseeventfMove = 0x0001;
    private const uint MouseeventfWheel = 0x0800;
    private const uint Xbutton1 = 0x0001;
    private const uint Xbutton2 = 0x0002;
    private const int WheelDelta = 120;

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    public Win32MouseEmulator(ISendInputChannel? sendChannel = null)
    {
        _sendChannel = sendChannel ?? new Win32SendInputChannel();
    }

    public void LeftDown() => SendMouseInput(MouseeventfLeftdown);
    public void LeftUp() => SendMouseInput(MouseeventfLeftup);
    public void LeftClick() => LeftClickAsync(CancellationToken.None).GetAwaiter().GetResult();

    public async Task LeftClickAsync(CancellationToken cancellationToken = default)
    {
        LeftDown();
        await Task.Delay(ClickHoldMs, cancellationToken).ConfigureAwait(false);
        LeftUp();
    }

    public void RightDown() => SendMouseInput(MouseeventfRightdown);
    public void RightUp() => SendMouseInput(MouseeventfRightup);
    public void RightClick() => RightClickAsync(CancellationToken.None).GetAwaiter().GetResult();

    public async Task RightClickAsync(CancellationToken cancellationToken = default)
    {
        RightDown();
        await Task.Delay(ClickHoldMs, cancellationToken).ConfigureAwait(false);
        RightUp();
    }

    public void MiddleDown() => SendMouseInput(MouseeventfMiddledown);
    public void MiddleUp() => SendMouseInput(MouseeventfMiddleup);
    public void MiddleClick() => MiddleClickAsync(CancellationToken.None).GetAwaiter().GetResult();

    public async Task MiddleClickAsync(CancellationToken cancellationToken = default)
    {
        MiddleDown();
        await Task.Delay(ClickHoldMs, cancellationToken).ConfigureAwait(false);
        MiddleUp();
    }

    public void X1Down() => SendMouseInput(MouseeventfXdown, Xbutton1);
    public void X1Up() => SendMouseInput(MouseeventfXup, Xbutton1);
    public void X1Click() => X1ClickAsync(CancellationToken.None).GetAwaiter().GetResult();

    public async Task X1ClickAsync(CancellationToken cancellationToken = default)
    {
        X1Down();
        await Task.Delay(ClickHoldMs, cancellationToken).ConfigureAwait(false);
        X1Up();
    }

    public void X2Down() => SendMouseInput(MouseeventfXdown, Xbutton2);
    public void X2Up() => SendMouseInput(MouseeventfXup, Xbutton2);
    public void X2Click() => X2ClickAsync(CancellationToken.None).GetAwaiter().GetResult();

    public async Task X2ClickAsync(CancellationToken cancellationToken = default)
    {
        X2Down();
        await Task.Delay(ClickHoldMs, cancellationToken).ConfigureAwait(false);
        X2Up();
    }

    public void WheelUp() => SendMouseInput(MouseeventfWheel, unchecked((uint)WheelDelta));
    public void WheelDown() => SendMouseInput(MouseeventfWheel, unchecked((uint)-WheelDelta));
    public void MoveBy(int deltaX, int deltaY) => SendMouseInput(MouseeventfMove, 0, deltaX, deltaY);

    private void SendMouseInput(uint flags, uint mouseData = 0, int dx = 0, int dy = 0)
    {
        var input = new INPUT
        {
            type = InputMouse,
            U = new InputUnion
            {
                mi = new MOUSEINPUT
                {
                    dx = dx,
                    dy = dy,
                    mouseData = mouseData,
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };

        var size = Marshal.SizeOf<INPUT>();
        var ptr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(input, ptr, false);
            var sent = _sendChannel.SendInput(1, (nint)ptr, size);
            if (sent != 1)
            {
                var err = Marshal.GetLastWin32Error();
                App.Logger.Warning($"Mouse SendInput failed. flags=0x{flags:X} err={err}");
            }
        }
        catch (Exception ex)
        {
            App.Logger.Error("Exception during Mouse SendInput", ex);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }
}
