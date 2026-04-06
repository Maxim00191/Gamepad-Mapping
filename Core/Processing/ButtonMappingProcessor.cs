using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Models;
using Vortice.XInput;

namespace GamepadMapperGUI.Core;

internal sealed class ButtonMappingProcessor
{
    private readonly HoldSessionManager _holdSessionManager;
    private readonly OutputStateTracker _outputStateTracker;
    private readonly ItemCycleProcessor _itemCycleProcessor;
    private readonly Func<bool> _canDispatchOutput;
    private readonly Action<string> _setMappedOutput;
    private readonly Action<string> _setMappingStatus;
    private readonly Action<string, TriggerMoment, DispatchedOutput, string, string> _queueOutputDispatch;
    private readonly Action<string, TriggerMoment, Key[], string, DispatchedOutput?, Key> _enqueueItemCycleTap;
    public delegate bool TryDispatchActionDelegate(
        MappingEntry mapping,
        TriggerMoment trigger,
        string sourceToken,
        out string? errorStatus);

    private readonly TryDispatchActionDelegate _tryDispatchAction;
    private readonly Func<IReadOnlyCollection<MappingEntry>, HashSet<GamepadButtons>> _resolveComboLeads;
    private readonly int _leadKeyReleaseSuppressMs;
    private readonly HashSet<GamepadButtons> _deferredSoloLeadButtons;
    private readonly object _deferredSoloLeadButtonsLock;
    private readonly ActiveActionTracker _activeActionTracker;

    public ButtonMappingProcessor(
        HoldSessionManager holdSessionManager,
        OutputStateTracker outputStateTracker,
        ItemCycleProcessor itemCycleProcessor,
        Func<bool> canDispatchOutput,
        Action<string> setMappedOutput,
        Action<string> setMappingStatus,
        Action<string, TriggerMoment, DispatchedOutput, string, string> queueOutputDispatch,
        Action<string, TriggerMoment, Key[], string, DispatchedOutput?, Key> enqueueItemCycleTap,
        TryDispatchActionDelegate tryDispatchAction,
        Func<IReadOnlyCollection<MappingEntry>, HashSet<GamepadButtons>> resolveComboLeads,
        int leadKeyReleaseSuppressMs,
        HashSet<GamepadButtons> deferredSoloLeadButtons,
        object deferredSoloLeadButtonsLock,
        ActiveActionTracker activeActionTracker)
    {
            _holdSessionManager = holdSessionManager;
            _outputStateTracker = outputStateTracker;
            _itemCycleProcessor = itemCycleProcessor;
            _canDispatchOutput = canDispatchOutput;
            _setMappedOutput = setMappedOutput;
            _setMappingStatus = setMappingStatus;
            _queueOutputDispatch = queueOutputDispatch;
            _enqueueItemCycleTap = enqueueItemCycleTap;
            _tryDispatchAction = tryDispatchAction;
            _resolveComboLeads = resolveComboLeads;
            _leadKeyReleaseSuppressMs = leadKeyReleaseSuppressMs;
            _deferredSoloLeadButtons = deferredSoloLeadButtons;
            _deferredSoloLeadButtonsLock = deferredSoloLeadButtonsLock;
            _activeActionTracker = activeActionTracker;
        }

        public void ProcessButtonEventTerminal(ButtonEventContext context)
        {
            if (context.IsSuppressed)
                return;

        if (context.Trigger == TriggerMoment.Released && 
            _activeActionTracker.IsButtonSuppressedByActiveChord(context.Button))
        {
            // Even if suppressed, we must ensure the button is unregistered from any tracking
            // to prevent stale state from blocking future presses.
            _activeActionTracker.ProcessButtonReleased(context.Button);
            
            // CRITICAL: DO NOT return here. We must allow the release event to flow down
            // to _holdSessionManager and other cleanup logic, otherwise they will
            // think the button is still being held (ghost state).
        }

        if (context.Trigger == TriggerMoment.Released &&
            _holdSessionManager.TryFinalizeDualActionOnRelease(
                context.Button,
                context.ReleasedButtonHeldMs,
                _resolveComboLeads(context.MappingsSnapshot).Contains(context.Button)))
        {
            lock (_deferredSoloLeadButtonsLock)
                _deferredSoloLeadButtons.Remove(context.Button);
            return;
        }

            var comboLeads = _resolveComboLeads(context.MappingsSnapshot);

            lock (_deferredSoloLeadButtonsLock)
            {
                if (context.Trigger == TriggerMoment.Released &&
                    _deferredSoloLeadButtons.Remove(context.Button))
                {
                    var heldMs = context.ReleasedButtonHeldMs;
                    if (heldMs.HasValue && heldMs.Value > _leadKeyReleaseSuppressMs)
                    {
                        _setMappingStatus($"Suppressed solo ({context.ButtonName}) - lead held past {_leadKeyReleaseSuppressMs} ms");
                    }
                    else if (_canDispatchOutput())
                    {
                        if (TryDispatchDeferredSoloShortRelease(context.Button, context.MappingsSnapshot))
                        {
                            context.DeferredSoloLeadHandledOnRelease = true;
                            _setMappingStatus($"Deferred solo (short): {context.ButtonName}");
                        }
                    }
                }
            }

            var matched = false;
            var suppressedByHoldDual = false;
            var candidates = new List<(MappingEntry Mapping, List<GamepadButtons> ChordButtons, bool RequiresRightTrigger, bool RequiresLeftTrigger, string SourceToken)>();

            foreach (var mapping in context.MappingsSnapshot)
            {
                if (mapping?.From is null) continue;
                if (mapping.From.Type != GamepadBindingType.Button) continue;
                if (!ChordResolver.TryParseButtonChord(mapping.From.Value, out var chordButtons, out var reqRt, out var reqLt, out var sourceToken))
                    continue;
                var triggerThreshold = GetTriggerMatchThreshold(mapping);
                if (!ChordResolver.DoesChordMatchEvent(
                        chordButtons,
                        reqRt,
                        reqLt,
                        context.LeftTriggerValue,
                        context.RightTriggerValue,
                        triggerThreshold,
                        context.Button,
                        context.ActiveButtons))
                    continue;
                if (mapping.Trigger != context.Trigger) continue;

                if (context.Trigger == TriggerMoment.Tap && chordButtons.Count == 1 &&
                    !mapping.RequiresDeferralOnPress &&
                    context.ActiveButtons.Any(b =>
                        b != context.Button && IsEffectiveSoloChordDeferLead(b, comboLeads, context.MappingsSnapshot)))
                {
                    _setMappingStatus($"Suppressed Tap for {context.ButtonName} - Lead button held");
                    continue;
                }

                if (context.Trigger == TriggerMoment.Pressed && chordButtons.Count == 1 &&
                    !mapping.RequiresDeferralOnPress &&
                    context.ActiveButtons.Any(b =>
                        b != context.Button && IsEffectiveSoloChordDeferLead(b, comboLeads, context.MappingsSnapshot)))
                {
                    _setMappingStatus($"Suppressed Pressed for {context.ButtonName} - Lead button held");
                    continue;
                }

                lock (_deferredSoloLeadButtonsLock)
                {
                    if (context.Trigger == TriggerMoment.Pressed &&
                        chordButtons.Count == 1 &&
                        !reqRt &&
                        !reqLt)
                    {
                        var multiChordDefer =
                            SnapshotHasMultiButtonChordContaining(chordButtons[0], context.MappingsSnapshot) &&
                            (comboLeads.Contains(chordButtons[0]) || mapping.RequiresDeferralOnPress);
                        if (multiChordDefer)
                        {
                            _deferredSoloLeadButtons.Add(context.Button);
                            continue;
                        }

                        if (HoldSessionManager.HasSoloKeyboardDeferredConflict(chordButtons[0], context.MappingsSnapshot))
                            continue;
                    }
                }
                if (context.Trigger == TriggerMoment.Released &&
                    context.DeferredSoloLeadHandledOnRelease &&
                    chordButtons.Count == 1 &&
                    !reqRt &&
                    !reqLt &&
                    chordButtons[0] == context.Button)
                    continue;
                if (context.Trigger == TriggerMoment.Released &&
                    ShouldSuppressLeadKeyReleasedOutput(chordButtons, context.ReleasedButtonHeldMs, context.MappingsSnapshot, comboLeads))
                    continue;
                if (context.Trigger == TriggerMoment.Tap && _holdSessionManager.HasHoldSessionForSourceToken(sourceToken))
                    continue;
                if (HoldSessionManager.IsHoldDualMapping(mapping))
                {
                    suppressedByHoldDual = true;
                    continue;
                }

                candidates.Add((mapping, chordButtons, reqRt, reqLt, sourceToken));
            }

            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                var hasMoreSpecificMatch = candidates.Any(other =>
                    !ReferenceEquals(other.Mapping, candidate.Mapping) &&
                    ChordResolver.IsOtherChordStrictlyMoreSpecific(
                        candidate.ChordButtons,
                        candidate.RequiresRightTrigger,
                        candidate.RequiresLeftTrigger,
                        other.ChordButtons,
                        other.RequiresRightTrigger,
                        other.RequiresLeftTrigger));
                if (hasMoreSpecificMatch)
                    continue;

                matched = true;

                // If we matched a chord (specificity >= 2), we MUST clear the deferred solo lead status
                // for ALL buttons participating in this chord, to prevent them from firing their solo
                // actions when they are eventually released.
                if (ChordResolver.ChordSpecificity(candidate.ChordButtons, candidate.RequiresRightTrigger, candidate.RequiresLeftTrigger) >= 2)
                {
                    lock (_deferredSoloLeadButtonsLock)
                    {
                        foreach (var b in candidate.ChordButtons)
                            _deferredSoloLeadButtons.Remove(b);
                    }
                }

                try
                {
                    if (_tryDispatchAction(
                            candidate.Mapping,
                            context.Trigger,
                            candidate.SourceToken,
                            out var actionErr))
                    {
                        if (actionErr is not null)
                            _setMappingStatus(actionErr);
                        continue;
                    }

                    if (!InputTokenResolver.TryResolveMappedOutput(candidate.Mapping.KeyboardKey, out var output, out var baseLabel))
                        continue;

                    _outputStateTracker.TrackOutputHoldState(candidate.SourceToken, candidate.ChordButtons, output, context.Trigger);
                    var outputLabel = $"{baseLabel} ({context.Trigger})";
                    _setMappedOutput(outputLabel);
                    _setMappingStatus($"Queued: {candidate.SourceToken} ({context.Trigger}) -> {outputLabel}");
                    _queueOutputDispatch(candidate.SourceToken, context.Trigger, output, outputLabel, candidate.Mapping.KeyboardKey ?? string.Empty);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to send key mapping. key={candidate.Mapping.KeyboardKey}, ex={ex.Message}");
                    _setMappingStatus($"Error sending '{candidate.Mapping.KeyboardKey}': {ex.Message}");
                }
            }

            var holdArmed = false;
            if (context.Trigger == TriggerMoment.Pressed)
                holdArmed = _holdSessionManager.TryArmHoldBinding(
                    context.Button,
                    context.ActiveButtons,
                    context.MappingsSnapshot,
                    context.LeftTriggerValue,
                    context.RightTriggerValue);

            var deferredConflictArmed = false;
            if (context.Trigger == TriggerMoment.Pressed &&
                HoldSessionManager.TryGetSoloKeyboardDeferredConflict(context.Button, context.MappingsSnapshot, out var kbMapping, out var defMapping))
            {
                var skipDeferredArm = false;
                lock (_deferredSoloLeadButtonsLock)
                    skipDeferredArm = _deferredSoloLeadButtons.Contains(context.Button);
                if (!skipDeferredArm)
                {
                    deferredConflictArmed = _holdSessionManager.TryArmDualActionSession(
                        context.Button,
                        context.ActiveButtons,
                        context.MappingsSnapshot,
                        context.LeftTriggerValue,
                        context.RightTriggerValue,
                        kbMapping,
                        () => DispatchDeferredAction(defMapping, context.Button, context.MappingsSnapshot));

                    if (deferredConflictArmed)
                    {
                        lock (_deferredSoloLeadButtonsLock)
                            _deferredSoloLeadButtons.Add(context.Button);
                    }
                }
            }

            if (!matched && !suppressedByHoldDual && !holdArmed && !deferredConflictArmed)
                _setMappingStatus($"No mapping for {context.ButtonName} ({context.Trigger})");
        }

        private void DispatchDeferredAction(MappingEntry mapping, GamepadButtons button, IReadOnlyList<MappingEntry> snapshot)
        {
            if (!ChordResolver.TryParseButtonChord(mapping.From!.Value, out _, out _, out _, out var sourceToken))
                return;

            if (_tryDispatchAction(mapping, TriggerMoment.Pressed, sourceToken, out var actionErr))
            {
                if (actionErr is not null)
                    _setMappingStatus(actionErr);
            }
        }

        public HashSet<DispatchedOutput> CollectReleasedOutputsHandledByMappings(
            GamepadButtons changedButton,
            IReadOnlyCollection<GamepadButtons> activeButtons,
            IReadOnlyCollection<MappingEntry> snapshot,
            float leftTriggerValue,
            float rightTriggerValue,
            long? releasedButtonHeldMs)
        {
            var handledOutputs = new HashSet<DispatchedOutput>();
            var comboLeads = _resolveComboLeads(snapshot);
            foreach (var mapping in snapshot)
            {
                if (mapping?.From is null || mapping.From.Type != GamepadBindingType.Button)
                    continue;
                if (mapping.Trigger != TriggerMoment.Released)
                    continue;
                if (!ChordResolver.TryParseButtonChord(mapping.From.Value, out var chordButtons, out var reqRt, out var reqLt, out _))
                    continue;
                if (ShouldSuppressLeadKeyReleasedOutput(chordButtons, releasedButtonHeldMs, snapshot, comboLeads))
                    continue;
                var triggerThreshold = GetTriggerMatchThreshold(mapping);
                if (!ChordResolver.DoesChordMatchEvent(chordButtons, reqRt, reqLt, leftTriggerValue, rightTriggerValue, triggerThreshold, changedButton, activeButtons))
                    continue;
                if (!InputTokenResolver.TryResolveMappedOutput(mapping.KeyboardKey, out var output, out _))
                    continue;

                handledOutputs.Add(output);
            }

            return handledOutputs;
        }

        private bool ShouldSuppressLeadKeyReleasedOutput(
            IReadOnlyList<GamepadButtons> chordButtons,
            long? releasedButtonHeldMs,
            IReadOnlyCollection<MappingEntry> snapshot,
            HashSet<GamepadButtons> comboLeads)
        {
            if (releasedButtonHeldMs is null || releasedButtonHeldMs <= _leadKeyReleaseSuppressMs)
                return false;
            if (chordButtons.Count != 1)
                return false;
            if (!IsEffectiveSoloChordDeferLead(chordButtons[0], comboLeads, snapshot))
                return false;
            return SnapshotHasMultiButtonChordContaining(chordButtons[0], snapshot);
        }

        /// <summary>
        /// Combo leads from the profile plus any button that has a solo <see cref="TriggerMoment.Pressed"/> deferred
        /// binding (Radial, Toggle, Cycle) so they participate in defer / long-release suppress like modifier leads.
        /// </summary>
        private static bool IsEffectiveSoloChordDeferLead(
            GamepadButtons button,
            HashSet<GamepadButtons> comboLeads,
            IReadOnlyCollection<MappingEntry> snapshot) =>
            comboLeads.Contains(button) || HasDeferredActionSoloPressedMapping(button, snapshot);

        private static bool HasDeferredActionSoloPressedMapping(
            GamepadButtons button,
            IReadOnlyCollection<MappingEntry> snapshot)
        {
            foreach (var mapping in snapshot)
            {
                if (mapping?.From is null || mapping.From.Type != GamepadBindingType.Button)
                    continue;
                if (mapping.Trigger != TriggerMoment.Pressed)
                    continue;
                
                if (!mapping.RequiresDeferralOnPress)
                    continue;

                if (!ChordResolver.TryParseButtonChord(mapping.From.Value, out var chord, out var rt, out var lt, out _))
                    continue;
                if (rt || lt || chord.Count != 1 || chord[0] != button)
                    continue;
                return true;
            }

            return false;
        }

        private static bool SnapshotHasMultiButtonChordContaining(
            GamepadButtons button,
            IReadOnlyCollection<MappingEntry> snapshot)
        {
            foreach (var mapping in snapshot)
            {
                if (mapping?.From is null || mapping.From.Type != GamepadBindingType.Button)
                    continue;
                if (!ChordResolver.TryParseButtonChord(mapping.From.Value, out var chord, out var reqRt, out var reqLt, out _))
                    continue;
                if (!chord.Contains(button))
                    continue;
                // RT/LT + one button is not "multi-button" deferral (solo radial must open on press, not release).
                if (chord.Count < 2)
                    continue;
                if (ChordResolver.ChordSpecificity(chord, reqRt, reqLt) >= 2)
                    return true;
            }

            return false;
        }

        private void ClearDeferredSoloLeadsForRichChord(
            IReadOnlyList<GamepadButtons> chordButtons,
            bool requiresRightTrigger,
            bool requiresLeftTrigger)
        {
            if (ChordResolver.ChordSpecificity(chordButtons, requiresRightTrigger, requiresLeftTrigger) < 2)
                return;
            foreach (var b in chordButtons)
                _deferredSoloLeadButtons.Remove(b);
        }

        private bool TryDispatchDeferredSoloShortRelease(GamepadButtons button, IReadOnlyList<MappingEntry> snapshot)
        {
            lock (_deferredSoloLeadButtonsLock)
            {
                MappingEntry? pressed = null;
                MappingEntry? released = null;
                MappingEntry? tap = null;
                foreach (var mapping in snapshot)
                {
                    if (mapping?.From is null || mapping.From.Type != GamepadBindingType.Button)
                        continue;
                    if (!ChordResolver.TryParseButtonChord(mapping.From.Value, out var chord, out var rt, out var lt, out var token))
                        continue;
                    if (rt || lt || chord.Count != 1 || chord[0] != button)
                        continue;
                    switch (mapping.Trigger)
                    {
                        case TriggerMoment.Pressed:
                            pressed = mapping;
                            break;
                        case TriggerMoment.Released:
                            released = mapping;
                            break;
                        case TriggerMoment.Tap:
                            tap = mapping;
                            break;
                    }
                }

                try
                {
                    // 1. Handle Pressed + Released pair (simulating a full click for deferred solo)
                    if (pressed is not null &&
                        released is not null &&
                        string.Equals(pressed.KeyboardKey, released.KeyboardKey, StringComparison.Ordinal) &&
                        ChordResolver.TryParseButtonChord(pressed.From.Value, out _, out _, out _, out var pressToken) &&
                        InputTokenResolver.TryResolveMappedOutput(pressed.KeyboardKey, out var output, out var baseLabel))
                    {
                        var soloChord = new List<GamepadButtons> { button };
                        _outputStateTracker.TrackOutputHoldState(pressToken, soloChord, output, TriggerMoment.Pressed);
                        var pressLabel = $"{baseLabel} (Pressed)";
                        _setMappedOutput(pressLabel);
                        _queueOutputDispatch(pressToken, TriggerMoment.Pressed, output, pressLabel, pressed.KeyboardKey ?? string.Empty);

                        _outputStateTracker.TrackOutputHoldState(pressToken, soloChord, output, TriggerMoment.Released);
                        var relLabel = $"{baseLabel} (Released)";
                        _setMappedOutput(relLabel);
                        _queueOutputDispatch(pressToken, TriggerMoment.Released, output, relLabel, released.KeyboardKey ?? string.Empty);
                        return true;
                    }

                    // 2. Try execute deferred solo release via Command Pattern
                    if (pressed?.ExecutableAction is { } pressedAction &&
                        ChordResolver.TryParseButtonChord(pressed.From!.Value, out _, out _, out _, out var pToken))
                    {
                        if (pressedAction.TryExecuteDeferredSoloRelease(pToken, out var err))
                        {
                            if (err is not null) _setMappingStatus(err);
                            return true;
                        }
                    }

                    if (tap?.ExecutableAction is { } tapAction &&
                        ChordResolver.TryParseButtonChord(tap.From!.Value, out _, out _, out _, out var tToken))
                    {
                        if (tapAction.TryExecuteDeferredSoloRelease(tToken, out var err))
                        {
                            if (err is not null) _setMappingStatus(err);
                            return true;
                        }
                    }

                    // 3. Fallback to standard Tap dispatch for standard keyboard keys
                    if (pressed is not null &&
                        ChordResolver.TryParseButtonChord(pressed.From!.Value, out _, out _, out _, out var soloToken) &&
                        InputTokenResolver.TryResolveMappedOutput(pressed.KeyboardKey, out var soloOut, out var tapLabel))
                    {
                        var outLabel = $"{tapLabel} (Tap)";
                        _setMappedOutput(outLabel);
                        _setMappingStatus($"Queued: {soloToken} (Tap) -> {outLabel}");
                        _queueOutputDispatch(soloToken, TriggerMoment.Tap, soloOut, outLabel, pressed.KeyboardKey ?? string.Empty);
                        return true;
                    }

                    if (tap is not null &&
                        ChordResolver.TryParseButtonChord(tap.From!.Value, out _, out _, out _, out var tapTok) &&
                        InputTokenResolver.TryResolveMappedOutput(tap.KeyboardKey, out var tOut, out var bLab))
                    {
                        var ol = $"{bLab} (Tap)";
                        _setMappedOutput(ol);
                        _queueOutputDispatch(tapTok, TriggerMoment.Tap, tOut, ol, tap.KeyboardKey ?? string.Empty);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Deferred solo release failed: {ex.Message}");
                    _setMappingStatus($"Deferred solo error: {ex.Message}");
                }

                return false;
            }
        }

    private static float GetTriggerMatchThreshold(MappingEntry mapping) =>
        mapping.AnalogThreshold is > 0 and <= 1 ? mapping.AnalogThreshold.Value : 0.35f;
}
