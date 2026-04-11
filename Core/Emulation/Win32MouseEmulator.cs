using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Gamepad_Mapping;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Storage;
using GamepadMapperGUI.Interfaces.Services.Update;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Interfaces.Services.Radial;
using GamepadMapperGUI.Services.Infrastructure;
using GamepadMapperGUI.Services.Storage;
using GamepadMapperGUI.Services.Update;
using GamepadMapperGUI.Services.Input;
using GamepadMapperGUI.Services.Radial;

using GamepadMapperGUI.Services.Win32;
using static GamepadMapperGUI.Services.Win32.Win32InputConstants;

namespace GamepadMapperGUI.Core;

/// <summary>Mouse output via Win32 <c>SendInput</c> (<see cref="ISendInputChannel"/>).</summary>
public sealed class Win32MouseEmulator : IMouseEmulator
{
    private readonly ISendInputChannel _sendChannel;

    /// <summary>Brief down-hold before up, aligned with <see cref="Win32KeyboardEmulator"/> tap timing.</summary>
    private const int ClickHoldMs = 30;

    private const int WheelDelta = 120;

    public Win32MouseEmulator(ISendInputChannel? sendChannel = null)
    {
        _sendChannel = sendChannel ?? new Win32SendInputChannel();
    }

    public void LeftDown() => SendMouseInput(MOUSEEVENTF_LEFTDOWN);
    public void LeftUp() => SendMouseInput(MOUSEEVENTF_LEFTUP);
    public void LeftClick()
    {
        LeftDown();
        Thread.Sleep(ClickHoldMs);
        LeftUp();
    }

    public async Task LeftClickAsync(CancellationToken cancellationToken = default)
    {
        LeftDown();
        await Task.Delay(ClickHoldMs, cancellationToken).ConfigureAwait(false);
        LeftUp();
    }

    public void RightDown() => SendMouseInput(MOUSEEVENTF_RIGHTDOWN);
    public void RightUp() => SendMouseInput(MOUSEEVENTF_RIGHTUP);
    public void RightClick()
    {
        RightDown();
        Thread.Sleep(ClickHoldMs);
        RightUp();
    }

    public async Task RightClickAsync(CancellationToken cancellationToken = default)
    {
        RightDown();
        await Task.Delay(ClickHoldMs, cancellationToken).ConfigureAwait(false);
        RightUp();
    }

    public void MiddleDown() => SendMouseInput(MOUSEEVENTF_MIDDLEDOWN);
    public void MiddleUp() => SendMouseInput(MOUSEEVENTF_MIDDLEUP);
    public void MiddleClick()
    {
        MiddleDown();
        Thread.Sleep(ClickHoldMs);
        MiddleUp();
    }

    public async Task MiddleClickAsync(CancellationToken cancellationToken = default)
    {
        MiddleDown();
        await Task.Delay(ClickHoldMs, cancellationToken).ConfigureAwait(false);
        MiddleUp();
    }

    public void X1Down() => SendMouseInput(MOUSEEVENTF_XDOWN, XBUTTON1);
    public void X1Up() => SendMouseInput(MOUSEEVENTF_XUP, XBUTTON1);
    public void X1Click()
    {
        X1Down();
        Thread.Sleep(ClickHoldMs);
        X1Up();
    }

    public async Task X1ClickAsync(CancellationToken cancellationToken = default)
    {
        X1Down();
        await Task.Delay(ClickHoldMs, cancellationToken).ConfigureAwait(false);
        X1Up();
    }

    public void X2Down() => SendMouseInput(MOUSEEVENTF_XDOWN, XBUTTON2);
    public void X2Up() => SendMouseInput(MOUSEEVENTF_XUP, XBUTTON2);
    public void X2Click()
    {
        X2Down();
        Thread.Sleep(ClickHoldMs);
        X2Up();
    }

    public async Task X2ClickAsync(CancellationToken cancellationToken = default)
    {
        X2Down();
        await Task.Delay(ClickHoldMs, cancellationToken).ConfigureAwait(false);
        X2Up();
    }

    public void WheelUp() => SendMouseInput(MOUSEEVENTF_WHEEL, unchecked((uint)WheelDelta));
    public void WheelDown() => SendMouseInput(MOUSEEVENTF_WHEEL, unchecked((uint)-WheelDelta));
    public void MoveBy(int deltaX, int deltaY, float stickMagnitude = 1.0f) => SendMouseInput(MOUSEEVENTF_MOVE, 0, deltaX, deltaY);

    private void SendMouseInput(uint flags, uint mouseData = 0, int dx = 0, int dy = 0)
    {
        Span<INPUT> inputs = stackalloc INPUT[1];
        inputs[0] = new INPUT
        {
            type = INPUT_MOUSE,
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

        var sent = _sendChannel.SendInput(inputs);
        if (sent != 1)
        {
            var err = Marshal.GetLastWin32Error();
            App.Logger.Warning($"Mouse SendInput failed. flags=0x{flags:X} err={err}");
        }
    }
}


