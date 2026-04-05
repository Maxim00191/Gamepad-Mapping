using System;
using System.Windows.Input;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Core.Actions;

internal sealed class ItemCycleAction(
    MappingEntry mapping,
    ItemCycleProcessor processor,
    Func<bool> canDispatch,
    Action<string> setMappedOutput,
    Action<string> setMappingStatus,
    Action<string, TriggerMoment, Key[], string, DispatchedOutput?, Key> enqueueItemCycleTap) : IExecutableAction
{
    public bool Execute(TriggerMoment trigger, string sourceToken, out string? errorStatus)
    {
        errorStatus = null;
        if (mapping.ItemCycle is not { } cycle)
            return false;

        if (trigger == TriggerMoment.Released)
            return true;

        if (!canDispatch())
            return true;

        if (!processor.TryPrepareItemCycleStep(
                cycle,
                out var digitKey,
                out var customOut,
                out var useCustomOut,
                out var modifierKeys,
                out var itemLabel,
                out var itemCycleError))
        {
            errorStatus = itemCycleError;
            return true;
        }

        setMappedOutput(itemLabel);
        setMappingStatus($"Queued: {sourceToken} ({trigger}) -> {itemLabel}");
        enqueueItemCycleTap(
            sourceToken,
            trigger,
            modifierKeys,
            itemLabel,
            useCustomOut ? customOut : null,
            useCustomOut ? Key.None : digitKey);
        return true;
    }

    public bool TryExecuteDeferredSoloRelease(string sourceToken, out string? errorStatus)
    {
        return Execute(TriggerMoment.Tap, sourceToken, out errorStatus);
    }

    public bool RequiresDeferralOnPress => true;
}
