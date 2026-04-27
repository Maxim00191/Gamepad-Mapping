using System;
using System.Collections.Generic;
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
/// When <see cref="IMouseSubMoveStepDelayer"/> is the immediate implementation, sub-moves run synchronously (legacy tests).
/// With <see cref="HardwareStyledMouseSubMoveStepDelayer"/>, sub-moves are emitted over wall-clock time on per-scope background workers using a per-batch <see cref="IMouseSubMoveScheduleSession"/> so injections are spaced within a poll-interval budget (HID-like cadence, not a single burst). Enqueueing does not block the gamepad reader thread on prior sub-move delays.
/// </remarks>
public sealed partial class HumanizingMouseEmulator : IMouseEmulator, IPendingMouseSubdivisionState
{
    internal enum MouseLookPlanKind
    {
        None,
        Direct,
        Subdivided,
    }

    private readonly IMouseEmulator _inner;
    private readonly IHumanInputNoiseController _noise;
    private readonly IMouseSubMoveStepDelayer _subMoveDelayer;
    private readonly bool _useAsyncSubMoveScheduling;

    private const int ClickHoldMs = 30;

    private readonly object _subMoveSync = new();

    private int _carryUnscopedX;
    private int _carryUnscopedY;
    private int _carryLeftX;
    private int _carryLeftY;
    private int _carryRightX;
    private int _carryRightY;

    private readonly bool[] _skipMergeOnSubMoveExit = new bool[MouseLookMotionConstraints.SubMoveSubdivisionScopeCount];
    private readonly object _asyncLatestBatchCtsSyncRoot = new();

    public HumanizingMouseEmulator(
        IMouseEmulator inner,
        IHumanInputNoiseController noise,
        IMouseSubMoveStepDelayer? subMoveDelayer = null)
    {
        _inner = inner;
        _noise = noise;
        _subMoveDelayer = subMoveDelayer ?? ImmediateMouseSubMoveStepDelayer.Instance;
        _useAsyncSubMoveScheduling = _subMoveDelayer is not ImmediateMouseSubMoveStepDelayer;
        _asyncInFlightCount = new int[MouseLookMotionConstraints.SubMoveSubdivisionScopeCount];
        if (_useAsyncSubMoveScheduling)
        {
            int n = MouseLookMotionConstraints.SubMoveSubdivisionScopeCount;
            _asyncMoveQueues = new System.Collections.Concurrent.BlockingCollection<AsyncMoveJob>[n];
            _asyncLatestBatchCts = new CancellationTokenSource?[n];
            _asyncCurrentlyExecutingBatchCts = new CancellationTokenSource?[n];
            _asyncScopeIdleEvents = new System.Threading.ManualResetEventSlim[n];
            for (int i = 0; i < n; i++)
            {
                _asyncMoveQueues[i] = new System.Collections.Concurrent.BlockingCollection<AsyncMoveJob>(
                    new System.Collections.Concurrent.ConcurrentQueue<AsyncMoveJob>());
                _asyncScopeIdleEvents[i] = new System.Threading.ManualResetEventSlim(initialState: true);
            }
        }
        else
        {
            _asyncMoveQueues = null;
            _asyncLatestBatchCts = null;
            _asyncScopeIdleEvents = null;
        }
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
        if (_useAsyncSubMoveScheduling)
        {
            EnqueueAsyncMove(deltaX, deltaY, stickMagnitude, moveSubdivisionScope);
            return;
        }

        int tx;
        int ty;
        lock (_subMoveSync)
        {
            if (deltaX != 0 || deltaY != 0)
            {
                var (cx, cy) = GetCarry(moveSubdivisionScope);
                deltaX += cx;
                deltaY += cy;
                SetCarry(moveSubdivisionScope, 0, 0);
            }

            var plan = TryPlanMouseLookMove(deltaX, deltaY, stickMagnitude, out tx, out ty, out int nSteps);
            switch (plan)
            {
                case MouseLookPlanKind.None:
                    return;
                case MouseLookPlanKind.Direct:
                    _inner.MoveBy(tx, ty, stickMagnitude, null);
                    return;
                case MouseLookPlanKind.Subdivided:
                    MoveByDistributed(tx, ty, nSteps, moveSubdivisionScope);
                    return;
            }
        }
    }

    /// <summary>
    /// Shared mouse-look plan after carry has been merged into <paramref name="mergedDeltaX"/> / <paramref name="mergedDeltaY"/> and carry cleared.
    /// </summary>
    internal MouseLookPlanKind TryPlanMouseLookMove(
        int mergedDeltaX,
        int mergedDeltaY,
        float stickMagnitude,
        out int totalX,
        out int totalY,
        out int nSteps)
    {
        var (jx, jy) = _noise.AdjustMouseMove(0, 0, stickMagnitude);

        totalX = mergedDeltaX + jx;
        totalY = mergedDeltaY + jy;

        if (totalX == 0 && totalY == 0)
        {
            nSteps = 0;
            return MouseLookPlanKind.None;
        }

        int span = Math.Max(Math.Abs(totalX), Math.Abs(totalY));
        if (span < MouseLookMotionConstraints.MinPixelSpanToSubdivide)
        {
            nSteps = 0;
            return MouseLookPlanKind.Direct;
        }

        nSteps = Math.Min(
            MouseLookMotionConstraints.MaxSmoothSubSteps,
            Math.Max(1, (span + MouseLookMotionConstraints.MaxChebyshevPixelsPerSubMove - 1) / MouseLookMotionConstraints.MaxChebyshevPixelsPerSubMove));

        return MouseLookPlanKind.Subdivided;
    }

    public void ClearPendingSubdivision(GamepadBindingType? thumbstickScope = null)
    {
        if (thumbstickScope is null)
        {
            for (int i = 0; i < MouseLookMotionConstraints.SubMoveSubdivisionScopeCount; i++)
                ClearSubMoveScopeAndCarry(i);
            return;
        }

        ClearSubMoveScopeAndCarry(ScopeIndex(thumbstickScope.Value));
    }

    private void ClearSubMoveScopeAndCarry(int scopeIndex)
    {
        _skipMergeOnSubMoveExit[scopeIndex] = true;
        if (_useAsyncSubMoveScheduling)
        {
            DrainAndCancelAsyncScopeQueue(scopeIndex);
            WaitForAsyncScopeQuiescent(scopeIndex);
        }

        _skipMergeOnSubMoveExit[scopeIndex] = false;

        lock (_subMoveSync)
        {
            switch (scopeIndex)
            {
                case 0:
                    _carryUnscopedX = 0;
                    _carryUnscopedY = 0;
                    break;
                case 1:
                    _carryLeftX = 0;
                    _carryLeftY = 0;
                    break;
                case 2:
                    _carryRightX = 0;
                    _carryRightY = 0;
                    break;
            }
        }
    }

    private void MoveByDistributed(int totalX, int totalY, int nSteps, GamepadBindingType? moveSubdivisionScope)
    {
        var built = BuildSubMoveStepList(totalX, totalY, nSteps);
        if (built.Steps.Count == 0)
            return;

        int scopeIndex = ScopeIndex(moveSubdivisionScope);
        RunSubMoveStepBatchAsync(totalX, totalY, built.Steps, moveSubdivisionScope, scopeIndex, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }

    private static (List<(int sx, int sy)> Steps, int AccX, int AccY, int StepsThisPoll) BuildSubMoveStepList(int totalX, int totalY, int nSteps)
    {
        int stepsThisPoll = Math.Min(nSteps, MouseLookMotionConstraints.MaxSubMovesPerGamepadPoll);
        int accX = 0;
        int accY = 0;
        var steps = new List<(int sx, int sy)>(stepsThisPoll);
        for (int i = 1; i <= stepsThisPoll; i++)
        {
            int nextX = (int)((long)totalX * i / nSteps);
            int nextY = (int)((long)totalY * i / nSteps);
            int sx = nextX - accX;
            int sy = nextY - accY;
            accX = nextX;
            accY = nextY;
            if (sx != 0 || sy != 0)
                steps.Add((sx, sy));
        }

        return (steps, accX, accY, stepsThisPoll);
    }

    /// <summary>
    /// Emits one batch of relative sub-moves with delayer spacing (hardware-style or zero-delay immediate).
    /// Merges any unsent remainder of <paramref name="totalX"/>/<paramref name="totalY"/> into carry when appropriate.
    /// </summary>
    private async Task RunSubMoveStepBatchAsync(
        int totalX,
        int totalY,
        List<(int sx, int sy)> steps,
        GamepadBindingType? scope,
        int scopeIndex,
        CancellationToken cancellationToken)
    {
        int sentX = 0;
        int sentY = 0;
        IMouseSubMoveScheduleSession schedule = _subMoveDelayer.BeginScheduleSession(steps.Count, cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            for (int i = 0; i < steps.Count; i++)
            {
                if (i > 0)
                    await schedule.DelayBeforeNextSubMoveAsync(cancellationToken).ConfigureAwait(false);

                cancellationToken.ThrowIfCancellationRequested();

                if (Volatile.Read(ref _skipMergeOnSubMoveExit[scopeIndex]))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    break;
                }

                var (sx, sy) = steps[i];
                _inner.MoveBy(sx, sy, 1.0f, null);
                sentX += sx;
                sentY += sy;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected when superseded or mapping reset.
        }
        finally
        {
            bool skipMerge = Volatile.Read(ref _skipMergeOnSubMoveExit[scopeIndex]);
            if (!skipMerge)
            {
                lock (_subMoveSync)
                {
                    var (cx, cy) = GetCarry(scope);
                    SetCarry(scope, cx + totalX - sentX, cy + totalY - sentY);
                }
            }
        }
    }

    private static int ScopeIndex(GamepadBindingType? scope) =>
        scope switch
        {
            GamepadBindingType.LeftThumbstick => 1,
            GamepadBindingType.RightThumbstick => 2,
            _ => 0,
        };

    private static GamepadBindingType? ScopeIndexToBinding(int scopeIndex) =>
        scopeIndex switch
        {
            1 => GamepadBindingType.LeftThumbstick,
            2 => GamepadBindingType.RightThumbstick,
            _ => null,
        };

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
