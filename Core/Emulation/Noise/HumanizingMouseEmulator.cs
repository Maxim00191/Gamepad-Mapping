using System;
using System.Threading;
using System.Threading.Tasks;
using GamepadMapperGUI.Interfaces.Services.Input;

namespace GamepadMapperGUI.Core.Emulation.Noise;

/// <summary>
/// Decorates <see cref="IMouseEmulator"/> with human-noise on <see cref="LeftClickAsync"/> (and other <c>*Async</c> full clicks) and on <see cref="MoveBy"/>.
/// Synchronous <c>*Click</c> methods forward to the inner emulator so hold timing is not duplicated here; prefer <c>*Async</c> from dispatch paths so delays use <see cref="Task.Delay"/> instead of blocking the caller thread.
/// </summary>
public sealed class HumanizingMouseEmulator : IMouseEmulator
{
    private readonly IMouseEmulator _inner;
    private readonly IHumanInputNoiseController _noise;

    private const int ClickHoldMs = 30;

    public HumanizingMouseEmulator(IMouseEmulator inner, IHumanInputNoiseController noise)
    {
        _inner = inner;
        _noise = noise;
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

    public void MoveBy(int deltaX, int deltaY)
    {
        var (dx, dy) = _noise.AdjustMouseMove(deltaX, deltaY);
        _inner.MoveBy(dx, dy);
    }

    private async Task ClickAsync(Action down, Action up, CancellationToken cancellationToken)
    {
        down();
        await Task.Delay(_noise.AdjustDelayMs(ClickHoldMs), cancellationToken).ConfigureAwait(false);
        up();
    }
}
