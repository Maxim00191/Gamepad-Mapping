using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Core;

internal sealed class InputDispatcher : IDisposable
{
    private readonly Action<DispatchedOutput, TriggerMoment> _dispatchMappedOutput;
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
        Action<Action> runOnUi,
        Action<string> setMappedOutput,
        Action<string> setMappingStatus)
    {
        _dispatchMappedOutput = dispatchMappedOutput;
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
            _outputQueue.Enqueue(new QueuedOutputWork(buttonName, trigger, output, outputLabel, sourceToken));
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
                _dispatchMappedOutput(workItem.Output, workItem.Trigger);
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
