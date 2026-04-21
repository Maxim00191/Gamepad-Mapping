using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Core.Emulation.Noise;

public sealed partial class HumanizingMouseEmulator
{
    private readonly BlockingCollection<AsyncMoveJob>?[]? _asyncMoveQueues;
    private readonly CancellationTokenSource?[]? _asyncLatestBatchCts;
    /// <summary>CTS for the batch currently executing on the worker (may differ from latest when a newer job is queued but not started).</summary>
    private readonly CancellationTokenSource?[]? _asyncCurrentlyExecutingBatchCts;
    private readonly int[] _asyncInFlightCount;
    /// <summary>Set when no async job is running for that scope (paired with <see cref="_asyncInFlightCount"/>).</summary>
    private readonly ManualResetEventSlim[]? _asyncScopeIdleEvents;

    private readonly struct AsyncMoveJob
    {
        public AsyncMoveJob(int deltaX, int deltaY, float stickMagnitude, GamepadBindingType? scope, CancellationTokenSource batchCts)
        {
            DeltaX = deltaX;
            DeltaY = deltaY;
            StickMagnitude = stickMagnitude;
            Scope = scope;
            BatchCts = batchCts;
        }

        public int DeltaX { get; }
        public int DeltaY { get; }
        public float StickMagnitude { get; }
        public GamepadBindingType? Scope { get; }
        public CancellationTokenSource BatchCts { get; }
    }

    private void EnsureAsyncMoveWorkersStarted()
    {
        if (_asyncWorkersStarted)
            return;
        lock (_asyncWorkerStartSync)
        {
            if (_asyncWorkersStarted)
                return;
            for (int i = 0; i < MouseLookMotionConstraints.SubMoveSubdivisionScopeCount; i++)
            {
                int scopeIndex = i;
                _ = Task.Factory.StartNew(
                    () => RunAsyncScopeWorkerLoop(scopeIndex),
                    CancellationToken.None,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);
            }

            _asyncWorkersStarted = true;
        }
    }

    private bool _asyncWorkersStarted;
    private readonly object _asyncWorkerStartSync = new();

    private void RunAsyncScopeWorkerLoop(int scopeIndex)
    {
        if (_asyncMoveQueues is null)
            return;

        var queue = _asyncMoveQueues[scopeIndex]!;
        foreach (var job in queue.GetConsumingEnumerable())
        {
            try
            {
                ProcessAsyncMoveJob(job, scopeIndex).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"HumanizingMouseEmulator scope {scopeIndex} worker: {ex}");
            }
        }
    }

    private async Task ProcessAsyncMoveJob(AsyncMoveJob job, int scopeIndex)
    {
        CancellationTokenSource?[]? executingArr = _asyncCurrentlyExecutingBatchCts;
        if (executingArr is null)
            return;

        NotifyAsyncScopeWorkStarted(scopeIndex);
        GamepadBindingType? scope = ScopeIndexToBinding(scopeIndex);
        CancellationTokenSource batchCts = job.BatchCts;
        CancellationToken cancellationToken = batchCts.Token;
        executingArr[scopeIndex] = batchCts;

        try
        {
            int totalX;
            int totalY;
            int nSteps;
            List<(int sx, int sy)> steps;
            MouseLookPlanKind plan;

            lock (_subMoveSync)
            {
                int deltaX = job.DeltaX;
                int deltaY = job.DeltaY;
                if (deltaX != 0 || deltaY != 0)
                {
                    var (cx, cy) = GetCarry(scope);
                    deltaX += cx;
                    deltaY += cy;
                    SetCarry(scope, 0, 0);
                }

                plan = TryPlanMouseLookMove(deltaX, deltaY, job.StickMagnitude, out totalX, out totalY, out nSteps);
                if (plan == MouseLookPlanKind.None)
                    return;

                if (plan == MouseLookPlanKind.Direct)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        var (cx, cy) = GetCarry(scope);
                        SetCarry(scope, cx + totalX, cy + totalY);
                        return;
                    }

                    _inner.MoveBy(totalX, totalY, job.StickMagnitude, null);
                    return;
                }

                var built = BuildSubMoveStepList(totalX, totalY, nSteps);
                steps = built.Steps;
                if (steps.Count == 0)
                {
                    var (cx, cy) = GetCarry(scope);
                    SetCarry(scope, cx + totalX, cy + totalY);
                    return;
                }
            }

            if (cancellationToken.IsCancellationRequested)
            {
                lock (_subMoveSync)
                {
                    var (cx, cy) = GetCarry(scope);
                    SetCarry(scope, cx + totalX, cy + totalY);
                }

                return;
            }

            await RunSubMoveStepBatchAsync(totalX, totalY, steps, scope, scopeIndex, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (ReferenceEquals(executingArr[scopeIndex], batchCts))
                executingArr[scopeIndex] = null;

            try
            {
                batchCts.Dispose();
            }
            catch
            {
                // Ignore late dispose.
            }

            NotifyAsyncScopeWorkEnded(scopeIndex);
        }
    }

    private void NotifyAsyncScopeWorkStarted(int scopeIndex)
    {
        if (Interlocked.Increment(ref _asyncInFlightCount[scopeIndex]) == 1)
            _asyncScopeIdleEvents?[scopeIndex].Reset();
    }

    private void NotifyAsyncScopeWorkEnded(int scopeIndex)
    {
        if (Interlocked.Decrement(ref _asyncInFlightCount[scopeIndex]) == 0)
            _asyncScopeIdleEvents?[scopeIndex].Set();
    }

    private void EnqueueAsyncMove(int deltaX, int deltaY, float stickMagnitude, GamepadBindingType? moveSubdivisionScope)
    {
        if (_asyncMoveQueues is null || _asyncLatestBatchCts is null)
            throw new InvalidOperationException("Async mouse dispatch is not initialized.");

        EnsureAsyncMoveWorkersStarted();

        int idx = ScopeIndex(moveSubdivisionScope);
        var batchCts = new CancellationTokenSource();
        lock (_asyncLatestBatchCtsSyncRoot)
        {
            CancellationTokenSource? previous = _asyncLatestBatchCts![idx];
            _asyncLatestBatchCts[idx] = batchCts;
            try
            {
                previous?.Cancel();
            }
            catch
            {
                // Ignore late cancel.
            }

            // Do not dispose previous here: the worker that owns that batch disposes its CTS in ProcessAsyncMoveJob.
        }

        var job = new AsyncMoveJob(deltaX, deltaY, stickMagnitude, moveSubdivisionScope, batchCts);
        _asyncMoveQueues[idx]!.Add(job);
    }

    private void WaitForAsyncScopeQuiescent(int scopeIndex)
    {
        if (_asyncMoveQueues is null || _asyncScopeIdleEvents is null)
            return;

        bool signaled = _asyncScopeIdleEvents[scopeIndex].Wait(
            TimeSpan.FromMilliseconds(MouseLookMotionConstraints.AsyncSubMoveScopeIdleWaitTimeoutMs));

        if (!signaled || Volatile.Read(ref _asyncInFlightCount[scopeIndex]) != 0)
        {
            for (int spin = 0; spin < 50_000; spin++)
            {
                if (Volatile.Read(ref _asyncInFlightCount[scopeIndex]) == 0)
                    return;
                Thread.Sleep(1);
            }
        }
    }

    private void DrainAndCancelAsyncScopeQueue(int scopeIndex)
    {
        if (_asyncMoveQueues is null)
            return;

        lock (_asyncLatestBatchCtsSyncRoot)
        {
            try
            {
                _asyncCurrentlyExecutingBatchCts![scopeIndex]?.Cancel();
            }
            catch
            {
                // Ignore.
            }

            try
            {
                _asyncLatestBatchCts![scopeIndex]?.Cancel();
            }
            catch
            {
                // Ignore.
            }
        }

        BlockingCollection<AsyncMoveJob> scopeQueue = _asyncMoveQueues[scopeIndex]!;
        while (scopeQueue.TryTake(out AsyncMoveJob job))
        {
            try
            {
                job.BatchCts.Cancel();
            }
            catch
            {
                // Ignore.
            }

            try
            {
                job.BatchCts.Dispose();
            }
            catch
            {
                // Ignore.
            }
        }
    }
}
