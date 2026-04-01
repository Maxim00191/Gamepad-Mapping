using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Core;

internal sealed class InputDispatcher : IDisposable
{
    private readonly Action<DispatchedOutput, TriggerMoment> _dispatchMappedOutput;
    private readonly Action<IReadOnlyList<Key>, Key> _dispatchChordTap;
    private readonly Action<Action> _runOnUi;
    private readonly Action<string> _setMappedOutput;
    private readonly Action<string> _setMappingStatus;
    private readonly object _outputQueueLock = new();
    private readonly Queue<QueuedOutputWork> _outputQueue = new();
    private readonly SemaphoreSlim _outputQueueSignal = new(0);
    private readonly CancellationTokenSource _outputQueueCts = new();
    private readonly Task _outputQueueWorkerTask;

    public InputDispatcher(
        Action<DispatchedOutput, TriggerMoment> dispatchMappedOutput,
        Action<IReadOnlyList<Key>, Key> dispatchChordTap,
        Action<Action> runOnUi,
        Action<string> setMappedOutput,
        Action<string> setMappingStatus)
    {
        _dispatchMappedOutput = dispatchMappedOutput;
        _dispatchChordTap = dispatchChordTap;
        _runOnUi = runOnUi;
        _setMappedOutput = setMappedOutput;
        _setMappingStatus = setMappingStatus;
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

    public void Dispose()
    {
        _outputQueueCts.Cancel();
        _outputQueueSignal.Release();
        _outputQueueWorkerTask.Wait(500);
        _outputQueueSignal.Dispose();
        _outputQueueCts.Dispose();
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
                    continue;

                workItem = _outputQueue.Dequeue();
            }

            try
            {
                if (workItem.ChordMainKey is { } mainKey && workItem.ChordModifiers is not null)
                    _dispatchChordTap(workItem.ChordModifiers, mainKey);
                else if (workItem.DirectOutput is { } direct)
                    _dispatchMappedOutput(direct, workItem.Trigger);
                else
                    throw new InvalidOperationException("Queued output has neither direct output nor chord keys.");

                _runOnUi(() =>
                {
                    _setMappedOutput(workItem.OutputLabel);
                    _setMappingStatus($"Sent: {workItem.ButtonName} ({workItem.Trigger}) -> {workItem.OutputLabel}");
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to send mapped output. token={workItem.SourceToken}, ex={ex.Message}");
                _runOnUi(() => _setMappingStatus($"Error sending '{workItem.SourceToken}': {ex.Message}"));
            }
        }
    }
}
