using System;
using System.Threading;
using System.Threading.Tasks;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Core.Emulation.Noise;

/// <summary>
/// Decorates <see cref="IMouseEmulator"/> with human-noise on <see cref="LeftClickAsync"/> (and other <c>*Async</c> full clicks) and on <see cref="MoveBy"/>.
/// Large relative <see cref="MoveBy"/> calls are split into smaller relative steps so Win32/injected backends do not emit a single giant <c>SendInput</c> move.
/// Sub-moves are capped per gamepad poll (<see cref="MouseLookMotionConstraints.MaxSubMovesPerGamepadPoll"/>); remainder carries forward so movement is not lost.
/// Carry is tracked per <see cref="GamepadBindingType"/> thumbstick so clearing one stick (e.g. radial menu consuming the other) does not drop the active stick's remainder.
/// </summary>
/// <remarks>
/// Mouse-look output rate follows the gamepad sampling interval (see <see cref="GamepadInputStreamConstraints"/>), not a hardware mouse report rate.
/// Within one poll, multiple relative moves may be issued back-to-back; that differs from typical USB HID timing but reduces single-step teleports.
/// </remarks>
public sealed class HumanizingMouseEmulator : IMouseEmulator, IPendingMouseSubdivisionState
{
    private readonly IMouseEmulator _inner;
    private readonly IHumanInputNoiseController _noise;

    private const int ClickHoldMs = 30;

    private int _carryUnscopedX;
    private int _carryUnscopedY;
    private int _carryLeftX;
    private int _carryLeftY;
    private int _carryRightX;
    private int _carryRightY;

    public HumanizingMouseEmulator(IMouseEmulator inner, IHumanInputNoiseController noise)
    {
        _inner = inner;
        _noise = noise;
    }

    public void Execute(OutputCommand command)
    {
        switch (command.Type)
        {
            case OutputCommandType.PointerDown:
            case OutputCommandType.PointerUp:
            case OutputCommandType.PointerClick:
            case OutputCommandType.PointerWheel:
                _inner.Execute(command);
                break;
        }
    }

    public async Task ExecuteAsync(OutputCommand command, CancellationToken cancellationToken = default)
    {
        switch (command.Type)
        {
            case OutputCommandType.PointerDown:
            case OutputCommandType.PointerUp:
            case OutputCommandType.PointerWheel:
                await _inner.ExecuteAsync(command, cancellationToken).ConfigureAwait(false);
                break;
            case OutputCommandType.PointerClick:
                Action? down = command.PointerAction switch
                {
                    PointerAction.LeftClick => _inner.LeftDown,
                    PointerAction.RightClick => _inner.RightDown,
                    PointerAction.MiddleClick => _inner.MiddleDown,
                    PointerAction.X1Click => _inner.X1Down,
                    PointerAction.X2Click => _inner.X2Down,
                    _ => null
                };
                Action? up = command.PointerAction switch
                {
                    PointerAction.LeftClick => _inner.LeftUp,
                    PointerAction.RightClick => _inner.RightUp,
                    PointerAction.MiddleClick => _inner.MiddleUp,
                    PointerAction.X1Click => _inner.X1Up,
                    PointerAction.X2Click => _inner.X2Up,
                    _ => null
                };
                if (down != null && up != null)
                {
                    await ClickAsync(down, up, cancellationToken).ConfigureAwait(false);
                }
                break;
        }
    }

    public void LeftDown() => _inner.LeftDown();
    public void LeftUp() => _inner.LeftUp();

    public void LeftClick() => _inner.LeftClick();

    public Task LeftClickAsync(CancellationToken cancellationToken = default) =>
        ClickAsync(_inner.LeftDown, _inner.LeftUp, cancellationToken);

    public void RightDown() => _inner.RightDown();
    public void RightUp() => _inner.RightUp();

    public void RightClick() => _inner.RightClick();

    public Task RightClickAsync(CancellationToken cancellationToken = default) =>
        ClickAsync(_inner.RightDown, _inner.RightUp, cancellationToken);

    public void MiddleDown() => _inner.MiddleDown();
    public void MiddleUp() => _inner.MiddleUp();

    public void MiddleClick() => _inner.MiddleClick();

    public Task MiddleClickAsync(CancellationToken cancellationToken = default) =>
        ClickAsync(_inner.MiddleDown, _inner.MiddleUp, cancellationToken);

    public void X1Down() => _inner.X1Down();
    public void X1Up() => _inner.X1Up();

    public void X1Click() => _inner.X1Click();

    public Task X1ClickAsync(CancellationToken cancellationToken = default) =>
        ClickAsync(_inner.X1Down, _inner.X1Up, cancellationToken);

    public void X2Down() => _inner.X2Down();
    public void X2Up() => _inner.X2Up();

    public void X2Click() => _inner.X2Click();

    public Task X2ClickAsync(CancellationToken cancellationToken = default) =>
        ClickAsync(_inner.X2Down, _inner.X2Up, cancellationToken);

    public void WheelUp() => _inner.WheelUp();
    public void WheelDown() => _inner.WheelDown();

    public void MoveBy(int deltaX, int deltaY, float stickMagnitude = 1.0f, GamepadBindingType? moveSubdivisionScope = null)
    {
        if (deltaX != 0 || deltaY != 0)
        {
            var (cx, cy) = GetCarry(moveSubdivisionScope);
            deltaX += cx;
            deltaY += cy;
            SetCarry(moveSubdivisionScope, 0, 0);
        }

        var (jx, jy) = _noise.AdjustMouseMove(0, 0, stickMagnitude);

        int tx = deltaX + jx;
        int ty = deltaY + jy;

        if (tx == 0 && ty == 0)
            return;

        int span = Math.Max(Math.Abs(tx), Math.Abs(ty));
        if (span < MouseLookMotionConstraints.MinPixelSpanToSubdivide)
        {
            _inner.MoveBy(tx, ty, stickMagnitude, null);
            return;
        }

        int nSteps = Math.Min(
            MouseLookMotionConstraints.MaxSmoothSubSteps,
            Math.Max(1, (span + MouseLookMotionConstraints.MaxChebyshevPixelsPerSubMove - 1) / MouseLookMotionConstraints.MaxChebyshevPixelsPerSubMove));

        MoveByDistributed(tx, ty, nSteps, moveSubdivisionScope);
    }

    public void ClearPendingSubdivision(GamepadBindingType? thumbstickScope = null)
    {
        if (thumbstickScope is null)
        {
            _carryUnscopedX = 0;
            _carryUnscopedY = 0;
            _carryLeftX = 0;
            _carryLeftY = 0;
            _carryRightX = 0;
            _carryRightY = 0;
            return;
        }

        switch (thumbstickScope.Value)
        {
            case GamepadBindingType.LeftThumbstick:
                _carryLeftX = 0;
                _carryLeftY = 0;
                break;
            case GamepadBindingType.RightThumbstick:
                _carryRightX = 0;
                _carryRightY = 0;
                break;
        }
    }

    private void MoveByDistributed(int totalX, int totalY, int nSteps, GamepadBindingType? moveSubdivisionScope)
    {
        int stepsThisPoll = Math.Min(nSteps, MouseLookMotionConstraints.MaxSubMovesPerGamepadPoll);
        int accX = 0;
        int accY = 0;
        for (int i = 1; i <= stepsThisPoll; i++)
        {
            int nextX = (int)((long)totalX * i / nSteps);
            int nextY = (int)((long)totalY * i / nSteps);
            int sx = nextX - accX;
            int sy = nextY - accY;
            accX = nextX;
            accY = nextY;
            if (sx != 0 || sy != 0)
                _inner.MoveBy(sx, sy, 1.0f, null);
        }

        if (stepsThisPoll < nSteps)
        {
            var (cx, cy) = GetCarry(moveSubdivisionScope);
            SetCarry(moveSubdivisionScope, cx + totalX - accX, cy + totalY - accY);
        }
    }

    private (int cx, int cy) GetCarry(GamepadBindingType? scope) =>
        scope switch
        {
            GamepadBindingType.LeftThumbstick => (_carryLeftX, _carryLeftY),
            GamepadBindingType.RightThumbstick => (_carryRightX, _carryRightY),
            _ => (_carryUnscopedX, _carryUnscopedY),
        };

    private void SetCarry(GamepadBindingType? scope, int cx, int cy)
    {
        switch (scope)
        {
            case GamepadBindingType.LeftThumbstick:
                _carryLeftX = cx;
                _carryLeftY = cy;
                break;
            case GamepadBindingType.RightThumbstick:
                _carryRightX = cx;
                _carryRightY = cy;
                break;
            default:
                _carryUnscopedX = cx;
                _carryUnscopedY = cy;
                break;
        }
    }

    private async Task ClickAsync(Action down, Action up, CancellationToken cancellationToken)
    {
        down();
        await Task.Delay(_noise.AdjustDelayMs(ClickHoldMs), cancellationToken).ConfigureAwait(false);
        up();
    }
}
