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
using GamepadMapperGUI.Models;
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

    public void Execute(OutputCommand command)
    {
        switch (command.Type)
        {
            case OutputCommandType.PointerDown:
                switch (command.PointerAction)
                {
                    case PointerAction.LeftClick: LeftDown(); break;
                    case PointerAction.RightClick: RightDown(); break;
                    case PointerAction.MiddleClick: MiddleDown(); break;
                    case PointerAction.X1Click: X1Down(); break;
                    case PointerAction.X2Click: X2Down(); break;
                }
                break;
            case OutputCommandType.PointerUp:
                switch (command.PointerAction)
                {
                    case PointerAction.LeftClick: LeftUp(); break;
                    case PointerAction.RightClick: RightUp(); break;
                    case PointerAction.MiddleClick: MiddleUp(); break;
                    case PointerAction.X1Click: X1Up(); break;
                    case PointerAction.X2Click: X2Up(); break;
                }
                break;
            case OutputCommandType.PointerClick:
                switch (command.PointerAction)
                {
                    case PointerAction.LeftClick: LeftClick(); break;
                    case PointerAction.RightClick: RightClick(); break;
                    case PointerAction.MiddleClick: MiddleClick(); break;
                    case PointerAction.X1Click: X1Click(); break;
                    case PointerAction.X2Click: X2Click(); break;
                }
                break;
            case OutputCommandType.PointerWheel:
                switch (command.PointerAction)
                {
                    case PointerAction.WheelUp: WheelUp(); break;
                    case PointerAction.WheelDown: WheelDown(); break;
                }
                break;
        }
    }

    public async Task ExecuteAsync(OutputCommand command, CancellationToken cancellationToken = default)
    {
        switch (command.Type)
        {
            case OutputCommandType.PointerDown:
                switch (command.PointerAction)
                {
                    case PointerAction.LeftClick: LeftDown(); break;
                    case PointerAction.RightClick: RightDown(); break;
                    case PointerAction.MiddleClick: MiddleDown(); break;
                    case PointerAction.X1Click: X1Down(); break;
                    case PointerAction.X2Click: X2Down(); break;
                }
                break;
            case OutputCommandType.PointerUp:
                switch (command.PointerAction)
                {
                    case PointerAction.LeftClick: LeftUp(); break;
                    case PointerAction.RightClick: RightUp(); break;
                    case PointerAction.MiddleClick: MiddleUp(); break;
                    case PointerAction.X1Click: X1Up(); break;
                    case PointerAction.X2Click: X2Up(); break;
                }
                break;
            case OutputCommandType.PointerClick:
                switch (command.PointerAction)
                {
                    case PointerAction.LeftClick: await LeftClickAsync(cancellationToken).ConfigureAwait(false); break;
                    case PointerAction.RightClick: await RightClickAsync(cancellationToken).ConfigureAwait(false); break;
                    case PointerAction.MiddleClick: await MiddleClickAsync(cancellationToken).ConfigureAwait(false); break;
                    case PointerAction.X1Click: await X1ClickAsync(cancellationToken).ConfigureAwait(false); break;
                    case PointerAction.X2Click: await X2ClickAsync(cancellationToken).ConfigureAwait(false); break;
                }
                break;
            case OutputCommandType.PointerWheel:
                switch (command.PointerAction)
                {
                    case PointerAction.WheelUp: WheelUp(); break;
                    case PointerAction.WheelDown: WheelDown(); break;
                }
                break;
        }
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
    public void MoveBy(int deltaX, int deltaY, float stickMagnitude = 1.0f, GamepadBindingType? moveSubdivisionScope = null) =>
        SendMouseInput(MOUSEEVENTF_MOVE, 0, deltaX, deltaY);

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


