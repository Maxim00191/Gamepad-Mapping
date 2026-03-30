using System;
using System.Collections.Generic;
using System.Linq;
using GamepadMapperGUI.Models;
using Vortice.XInput;

namespace GamepadMapperGUI.Core;

internal sealed class OutputStateTracker
{
    private readonly Dictionary<string, HashSet<DispatchedOutput>> _activeHeldOutputsBySource = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<GamepadButtons, HashSet<string>> _activeHeldSourcesByButton = new();

    public void TrackOutputHoldState(
        string sourceToken,
        IReadOnlyCollection<GamepadButtons> sourceButtons,
        DispatchedOutput output,
        TriggerMoment trigger)
    {
        if (trigger == TriggerMoment.Pressed && IsHoldableOutput(output))
        {
            if (!_activeHeldOutputsBySource.TryGetValue(sourceToken, out var heldOutputs))
            {
                heldOutputs = [];
                _activeHeldOutputsBySource[sourceToken] = heldOutputs;
            }

            heldOutputs.Add(output);
            foreach (var sourceButton in sourceButtons)
            {
                if (!_activeHeldSourcesByButton.TryGetValue(sourceButton, out var sourceSet))
                {
                    sourceSet = [];
                    _activeHeldSourcesByButton[sourceButton] = sourceSet;
                }

                sourceSet.Add(sourceToken);
            }

            return;
        }

        if (trigger != TriggerMoment.Released)
            return;

        if (_activeHeldOutputsBySource.TryGetValue(sourceToken, out var existing))
        {
            existing.Remove(output);
            if (existing.Count == 0)
                RemoveTrackedHeldSource(sourceToken);
        }
    }

    public void ForceReleaseHeldOutputsForButton(
        GamepadButtons button,
        Action<DispatchedOutput> forceReleaseOutput,
        IReadOnlySet<DispatchedOutput>? outputsHandledByReleasedMappings = null)
    {
        if (!_activeHeldSourcesByButton.TryGetValue(button, out var sourceTokens) || sourceTokens.Count == 0)
            return;

        foreach (var sourceToken in sourceTokens.ToList())
        {
            if (!_activeHeldOutputsBySource.TryGetValue(sourceToken, out var heldOutputs))
                continue;

            foreach (var heldOutput in heldOutputs.ToList())
            {
                if (outputsHandledByReleasedMappings?.Contains(heldOutput) == true)
                    continue;
                forceReleaseOutput(heldOutput);
            }

            RemoveTrackedHeldSource(sourceToken);
        }
    }

    public void ForceReleaseAllOutputs(Action<DispatchedOutput> forceReleaseOutput)
    {
        foreach (var heldOutputs in _activeHeldOutputsBySource.Values)
        {
            foreach (var heldOutput in heldOutputs.ToList())
                forceReleaseOutput(heldOutput);
        }

        _activeHeldOutputsBySource.Clear();
        _activeHeldSourcesByButton.Clear();
    }

    private static bool IsHoldableOutput(DispatchedOutput output)
    {
        if (output.KeyboardKey is { } key && key != System.Windows.Input.Key.None)
            return true;

        return output.PointerAction is PointerAction.LeftClick
            or PointerAction.RightClick
            or PointerAction.MiddleClick
            or PointerAction.X1Click
            or PointerAction.X2Click;
    }

    private void RemoveTrackedHeldSource(string sourceToken)
    {
        _activeHeldOutputsBySource.Remove(sourceToken);
        foreach (var kvp in _activeHeldSourcesByButton.ToList())
        {
            kvp.Value.Remove(sourceToken);
            if (kvp.Value.Count == 0)
                _activeHeldSourcesByButton.Remove(kvp.Key);
        }
    }
}
