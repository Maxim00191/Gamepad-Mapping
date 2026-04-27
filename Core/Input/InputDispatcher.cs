using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Core;

internal sealed class InputDispatcher : IInputDispatcher, IDisposable
{
    private static readonly TimeSpan MappedOutputUiThrottle = TimeSpan.FromMilliseconds(50);

    private readonly Func<DispatchedOutput, TriggerMoment, CancellationToken, Task> _dispatchMappedOutputAsync;
    private readonly Func<IReadOnlyList<Key>, Key, CancellationToken, Task> _dispatchChordTapAsync;
    private readonly Action<string> _setMappedOutput;
    private readonly Action<string> _setMappingStatus;
    private readonly object _outputQueueLock = new();
    private readonly Queue<QueuedOutputWork> _outputQueue = new();
    private readonly SemaphoreSlim _outputQueueSignal = new(0);
    private readonly CancellationTokenSource _outputQueueCts = new();
    private readonly Task _outputQueueWorkerTask;
    private TaskCompletionSource _idleTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private string? _pendingMappedOutputLabel;
    private string? _pendingMappingStatus;
    private long _lastMappedOutputUiFlushTimestamp = Stopwatch.GetTimestamp();

    /// <param name="setMappedOutput">
    /// Monitor / status sink; must be safe to call from the output worker thread (typically marshals to the UI thread).
    /// </param>
    public InputDispatcher(
        Func<DispatchedOutput, TriggerMoment, CancellationToken, Task> dispatchMappedOutputAsync,
        Func<IReadOnlyList<Key>, Key, CancellationToken, Task> dispatchChordTapAsync,
        Action<string> setMappedOutput,
        Action<string> setMappingStatus)
    {
        _dispatchMappedOutputAsync = dispatchMappedOutputAsync;
        _dispatchChordTapAsync = dispatchChordTapAsync;
        _setMappedOutput = setMappedOutput;
        _setMappingStatus = setMappingStatus;

        _idleTcs.TrySetResult();

        _outputQueueWorkerTask = Task.Run(ProcessOutputQueueAsync);
    }

    public void Enqueue(
        string buttonName,
        TriggerMoment trigger,
        DispatchedOutput output,
        string outputLabel,
        string sourceToken)
    {
        lock (_outputQueueLock)
        {
            if (_outputQueue.Count >= 10000)
            {
                _outputQueue.Dequeue();
            }

            if (_outputQueue.Count == 0)
            {
                if (!_idleTcs.Task.IsCompleted)
                {
                    _idleTcs.TrySetResult();
                }
                _idleTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            _outputQueue.Enqueue(new QueuedOutputWork(
                buttonName,
                trigger,
                outputLabel,
                sourceToken,
                output,
                ChordModifiers: null,
                ChordMainKey: null));
        }

        _outputQueueSignal.Release();
    }

    public void EnqueueChordTap(
        string buttonName,
        TriggerMoment trigger,
        Key[] modifiers,
        Key mainKey,
        string outputLabel,
        string sourceToken)
    {
        lock (_outputQueueLock)
        {
            if (_outputQueue.Count >= 10000)
            {
                _outputQueue.Dequeue();
            }

            if (_outputQueue.Count == 0)
            {
                if (!_idleTcs.Task.IsCompleted)
                {
                    _idleTcs.TrySetResult();
                }
                _idleTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            _outputQueue.Enqueue(new QueuedOutputWork(
                buttonName,
                trigger,
                outputLabel,
                sourceToken,
                DirectOutput: null,
                ChordModifiers: modifiers,
                ChordMainKey: mainKey));
        }

        _outputQueueSignal.Release();
    }

    public Task WaitForIdleAsync()
    {
        lock (_outputQueueLock)
        {
            return _idleTcs.Task;
        }
    }

    public void Dispose()
    {
        CancellationTokenSource? ctsToCancel = null;
        Task? taskToWait = null;

        lock (_outputQueueLock)
        {
            if (_outputQueueCts.IsCancellationRequested) return;
            ctsToCancel = _outputQueueCts;
            taskToWait = _outputQueueWorkerTask;
            _outputQueueCts.Cancel();
        }
        
        _outputQueueSignal.Release();
        
        try
        {
            // Wait for worker to finish, but with a timeout to prevent hanging the UI thread
            taskToWait?.Wait(TimeSpan.FromMilliseconds(500));
        }
        catch (AggregateException) { } // Expected
        finally
        {
            ctsToCancel?.Dispose();
            _outputQueueSignal.Dispose();
        }
    }

    private async Task ProcessOutputQueueAsync()
    {
        while (!_outputQueueCts.IsCancellationRequested)
        {
            try
            {
                await _outputQueueSignal.WaitAsync(_outputQueueCts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            QueuedOutputWork workItem;
            lock (_outputQueueLock)
            {
                if (_outputQueue.Count == 0)
                {
                    _idleTcs.TrySetResult();
                    continue;
                }

                workItem = _outputQueue.Dequeue();
            }

            var dispatchSucceeded = false;
            try
            {
                if (workItem.ChordMainKey is { } mainKey && workItem.ChordModifiers is not null)
                    await _dispatchChordTapAsync(workItem.ChordModifiers, mainKey, _outputQueueCts.Token)
                        .ConfigureAwait(false);
                else if (workItem.DirectOutput is { } direct)
                    await _dispatchMappedOutputAsync(direct, workItem.Trigger, _outputQueueCts.Token)
                        .ConfigureAwait(false);
                else
                    throw new InvalidOperationException("Queued output has neither direct output nor chord keys.");

                _pendingMappedOutputLabel = workItem.OutputLabel;
                _pendingMappingStatus = $"Sent: {workItem.ButtonName} ({workItem.Trigger}) -> {workItem.OutputLabel}";
                dispatchSucceeded = true;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to send mapped output. token={workItem.SourceToken}, ex={ex.Message}");
                _setMappingStatus($"Error sending '{workItem.SourceToken}': {ex.Message}");
            }
            finally
            {
                lock (_outputQueueLock)
                {
                    if (_outputQueue.Count == 0)
                    {
                        _idleTcs.TrySetResult();
                    }
                }

                if (dispatchSucceeded)
                    FlushMappedOutputUiThrottled();
            }
        }
    }

    private void FlushMappedOutputUiThrottled()
    {
        bool queueEmpty;
        lock (_outputQueueLock)
        {
            queueEmpty = _outputQueue.Count == 0;
        }

        var elapsed = Stopwatch.GetElapsedTime(_lastMappedOutputUiFlushTimestamp);
        if (!queueEmpty && elapsed < MappedOutputUiThrottle)
            return;

        var label = _pendingMappedOutputLabel ?? string.Empty;
        var status = _pendingMappingStatus ?? string.Empty;
        _setMappedOutput(label);
        _setMappingStatus(status);
        _lastMappedOutputUiFlushTimestamp = Stopwatch.GetTimestamp();
    }
}
