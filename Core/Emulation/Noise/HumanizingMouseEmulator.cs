using System;
using System.Threading;
using System.Threading.Tasks;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Core.Emulation.Noise;

/// <summary>
/// Decorates <see cref="IMouseEmulator"/> with human-noise on <see cref="LeftClickAsync"/> (and other <c>*Async</c> full clicks) and on <see cref="MoveBy"/>.
/// Large relative <see cref="MoveBy"/> calls are split into smaller relative steps so Win32/injected backends do not emit a single giant <c>SendInput</c> move.
/// Synchronous <c>*Click</c> methods forward to the inner emulator so hold timing is not duplicated here; prefer <c>*Async</c> from dispatch paths so delays use <see cref="Task.Delay"/> instead of blocking the caller thread.
/// </summary>
public sealed class HumanizingMouseEmulator : IMouseEmulator
{
    private readonly IMouseEmulator _inner;
    private readonly IHumanInputNoiseController _noise;

    private const int ClickHoldMs = 30;

    /// <summary>Below this Chebyshev span, emit a single relative move (tiny steps are already fine-grained).</summary>
    private const int MinPixelSpanToSubdivide = 4;

    /// <summary>Upper bound on sub-moves per call to keep the input thread responsive.</summary>
    private const int MaxSmoothSubSteps = 48;

    /// <summary>Target max Chebyshev norm per sub-move when subdividing (smaller = smoother, more events).</summary>
    private const int MaxChebyshevPixelsPerSubMove = 2;

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

    public void MoveBy(int deltaX, int deltaY, float stickMagnitude = 1.0f)
    {
        // 1. Get noise-only jitter from the controller.
        // This is decoupled from the mechanical move delta but scaled by stick magnitude.
        var (jx, jy) = _noise.AdjustMouseMove(0, 0, stickMagnitude);

        int tx = deltaX + jx;
        int ty = deltaY + jy;

        if (tx == 0 && ty == 0)
            return;

        int span = Math.Max(Math.Abs(tx), Math.Abs(ty));
        if (span < MinPixelSpanToSubdivide)
        {
            _inner.MoveBy(tx, ty, stickMagnitude);
            return;
        }

        int nSteps = Math.Min(
            MaxSmoothSubSteps,
            Math.Max(1, (span + MaxChebyshevPixelsPerSubMove - 1) / MaxChebyshevPixelsPerSubMove));

        MoveByDistributed(tx, ty, nSteps);
    }

    private void MoveByDistributed(int totalX, int totalY, int nSteps)
    {
        int accX = 0;
        int accY = 0;
        for (int i = 1; i <= nSteps; i++)
        {
            int nextX = (int)((long)totalX * i / nSteps);
            int nextY = (int)((long)totalY * i / nSteps);
            int sx = nextX - accX;
            int sy = nextY - accY;
            accX = nextX;
            accY = nextY;
            if (sx != 0 || sy != 0)
                _inner.MoveBy(sx, sy, 1.0f);
        }
    }

    private async Task ClickAsync(Action down, Action up, CancellationToken cancellationToken)
    {
        down();
        await Task.Delay(_noise.AdjustDelayMs(ClickHoldMs), cancellationToken).ConfigureAwait(false);
        up();
    }
}
