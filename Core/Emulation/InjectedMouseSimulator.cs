using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Input.Preview.Injection;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Models;
using Gamepad_Mapping;

namespace GamepadMapperGUI.Core.Emulation;

/// <summary>
/// Mouse output via <see cref="InputInjector"/> (Windows.UI.Input.Preview.Injection).
/// </summary>
public sealed class InjectedMouseSimulator : IMouseEmulator, IVirtualScreenMouse
{
    private readonly InputInjector? _injector;
    private const int ClickHoldMs = 30;
    private const int WheelDelta = 120;

    public InjectedMouseSimulator()
    {
        try
        {
            _injector = InputInjector.TryCreate();
            if (_injector == null)
            {
                App.Logger.Error("InputInjector.TryCreate() returned null. Mouse injection will not work (requires UI access/trusted process).");
            }
        }
        catch (Exception ex)
        {
            App.Logger.Error($"Failed to initialize InputInjector: {ex.Message}");
            _injector = null;
        }
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

    public void LeftDown() => SendMouseInput(InjectedInputMouseOptions.LeftDown);
    public void LeftUp() => SendMouseInput(InjectedInputMouseOptions.LeftUp);
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

    public void RightDown() => SendMouseInput(InjectedInputMouseOptions.RightDown);
    public void RightUp() => SendMouseInput(InjectedInputMouseOptions.RightUp);
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

    public void MiddleDown() => SendMouseInput(InjectedInputMouseOptions.MiddleDown);
    public void MiddleUp() => SendMouseInput(InjectedInputMouseOptions.MiddleUp);
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

    public void X1Down() => SendMouseInput(InjectedInputMouseOptions.XDown, 1);
    public void X1Up() => SendMouseInput(InjectedInputMouseOptions.XUp, 1);
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

    public void X2Down() => SendMouseInput(InjectedInputMouseOptions.XDown, 2);
    public void X2Up() => SendMouseInput(InjectedInputMouseOptions.XUp, 2);
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

    public void WheelUp() => SendMouseInput(InjectedInputMouseOptions.Wheel, (uint)WheelDelta);
    public void WheelDown() => SendMouseInput(InjectedInputMouseOptions.Wheel, unchecked((uint)-WheelDelta));

    public void MoveBy(int deltaX, int deltaY, float stickMagnitude = 1.0f, GamepadBindingType? moveSubdivisionScope = null) =>
        SendMouseInput(InjectedInputMouseOptions.Move, 0, deltaX, deltaY);

    void IVirtualScreenMouse.MoveCursorToVirtualScreenPixels(int physicalX, int physicalY)
    {
    }

    private void SendMouseInput(InjectedInputMouseOptions options, uint mouseData = 0, int dx = 0, int dy = 0)
    {
        if (_injector == null) return;

        var info = new InjectedInputMouseInfo
        {
            MouseOptions = options,
            MouseData = mouseData,
            DeltaX = dx,
            DeltaY = dy
        };
        _injector.InjectMouseInput([info]);
    }
}
