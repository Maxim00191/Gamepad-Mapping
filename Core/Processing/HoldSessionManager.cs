using System;
using System.Collections.Generic;
using System.Linq;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Models;
using Vortice.XInput;
using ITimer = GamepadMapperGUI.Interfaces.Core.ITimer;

namespace GamepadMapperGUI.Core;

internal sealed class HoldSessionManager
{
    public const int DefaultModifierGraceMs = 500;

    private const int MinHoldThresholdMs = 150;
    private const int MaxHoldThresholdMs = 3000;

    private readonly Func<bool> _canDispatchOutput;
    private readonly Action<string> _setMappedOutput;
    private readonly Action<string> _setMappingStatus;
    private readonly Action<string, TriggerMoment, DispatchedOutput, string, string> _queueOutputDispatch;
    private readonly Action _onHoldSessionsChanged;
    private readonly Func<IReadOnlyCollection<MappingEntry>, HashSet<GamepadButtons>> _resolveComboLeads;
    private readonly ITimeProvider _timeProvider;
    private readonly int _modifierGraceMs;
    private readonly int _leadKeyReleaseSuppressMs;
    private readonly Action<Action>? _runSynchronizedWithInputFrame;
    private readonly Func<MappingEntry, string, bool>? _tryOpenRadialMenuFromKeyboardConflict;

    private readonly Dictionary<string, HoldSession> _holdSessions = new(StringComparer.Ordinal);
    private readonly Dictionary<GamepadButtons, RadialKeyboardConflictSession> _radialKeyboardConflictSessions = new();
    private readonly Dictionary<GamepadButtons, long> _buttonDownTicks = new();

    private IReadOnlyCollection<GamepadButtons> _latestActiveButtons = Array.Empty<GamepadButtons>();
    private float _latestLeftTrigger;
    private float _latestRightTrigger;

    internal HoldSessionManager(
        Func<bool> canDispatchOutput,
        Action<string> setMappedOutput,
        Action<string> setMappingStatus,
        Action<string, TriggerMoment, DispatchedOutput, string, string> queueOutputDispatch,
        Action onHoldSessionsChanged,
        Func<IReadOnlyCollection<MappingEntry>, HashSet<GamepadButtons>> resolveComboLeads,
        ITimeProvider timeProvider,
        int modifierGraceMs,
        int leadKeyReleaseSuppressMs,
        Action<Action>? runSynchronizedWithInputFrame = null,
        Func<MappingEntry, string, bool>? tryOpenRadialMenuFromKeyboardConflict = null)
    {
        _canDispatchOutput = canDispatchOutput;
        _setMappedOutput = setMappedOutput;
        _setMappingStatus = setMappingStatus;
        _queueOutputDispatch = queueOutputDispatch;
        _onHoldSessionsChanged = onHoldSessionsChanged;
        _resolveComboLeads = resolveComboLeads;
        _timeProvider = timeProvider;
        _modifierGraceMs = modifierGraceMs;
        _leadKeyReleaseSuppressMs = leadKeyReleaseSuppressMs;
        _runSynchronizedWithInputFrame = runSynchronizedWithInputFrame;
        _tryOpenRadialMenuFromKeyboardConflict = tryOpenRadialMenuFromKeyboardConflict;
    }

    /// <summary>
    /// Elapsed ms since <see cref="RegisterButtonPressed"/> for <paramref name="button"/>, or <c>null</c> if not tracked.
    /// </summary>
    public long? TryGetPressedDurationMs(GamepadButtons button)
    {
        lock (_buttonDownTicks)
        {
            if (!_buttonDownTicks.TryGetValue(button, out var downTick))
                return null;
            var now = _timeProvider.GetTickCount64();
            var delta = (long)((ulong)((ulong)now - (ulong)downTick));
            return delta;
        }
    }

    internal sealed class HoldSession
    {
        public required string SourceToken { get; init; }
        public required string ShortKeyToken { get; init; }
        public required string HoldKeyToken { get; init; }
        public required List<GamepadButtons> ChordButtons { get; init; }
        public required bool RequiresRightTrigger { get; init; }
        public required bool RequiresLeftTrigger { get; init; }
        public required float TriggerMatchThreshold { get; init; }
        public required int HoldThresholdMs { get; init; }
        public required ITimer Timer { get; init; }
        public bool LongFired { get; set; }
    }

    private sealed class RadialKeyboardConflictSession
    {
        public required GamepadButtons Button { get; init; }
        public required string SourceToken { get; init; }
        public required string ShortKeyToken { get; init; }
        public required MappingEntry RadialMapping { get; init; }
        public required ITimer Timer { get; init; }
        public bool RadialOpened { get; set; }
    }

    public void UpdateLatestInputState(
        IReadOnlyCollection<GamepadButtons> activeButtons,
        float leftTriggerValue,
        float rightTriggerValue)
    {
        _latestActiveButtons = activeButtons;
        _latestLeftTrigger = leftTriggerValue;
        _latestRightTrigger = rightTriggerValue;
    }

    public bool HasHoldSessionForSourceToken(string sourceToken) =>
        _holdSessions.ContainsKey(sourceToken);

    public bool TryGetFirstHoldSession(out HoldSession? session)
    {
        foreach (var s in _holdSessions.Values)
        {
            session = s;
            return true;
        }

        session = null;
        return false;
    }

    public int ActiveHoldSessionCount => _holdSessions.Count;

    /// <summary>
    /// Records when a physical button transition entered the pressed state (chord modifier grace and hold timing share this timeline).
    /// </summary>
    public void RegisterButtonPressed(GamepadButtons button)
    {
        lock (_buttonDownTicks)
        {
            _buttonDownTicks[button] = _timeProvider.GetTickCount64();
        }
    }

    public void RegisterButtonReleased(GamepadButtons button)
    {
        lock (_buttonDownTicks)
        {
            _buttonDownTicks.Remove(button);
        }
    }

    public void ClearButtonDownTicks()
    {
        lock (_buttonDownTicks)
        {
            _buttonDownTicks.Clear();
        }
    }

    /// <summary>
    /// Blocks chord Tap/Pressed when another chord member was already held longer than the configured modifier grace window.
    /// </summary>
    public bool ShouldSuppressChordForOverdueModifier(IReadOnlyList<GamepadButtons> chordButtons, GamepadButtons changedButton)
    {
        if (chordButtons.Count < 2)
            return false;

        var now = _timeProvider.GetTickCount64();
        lock (_buttonDownTicks)
        {
            foreach (var member in chordButtons)
            {
                if (member == changedButton)
                    continue;
                if (!_buttonDownTicks.TryGetValue(member, out var downTick))
                    continue;
                var heldMs = now - downTick;
                if (heldMs < 0)
                    continue;
                if (heldMs > _modifierGraceMs)
                    return true;
            }
        }

        return false;
    }

    public static bool IsHoldDualMapping(MappingEntry mapping)
    {
        if (mapping.ItemCycle != null)
            return false;
        if (mapping.TemplateToggle != null)
            return false;
        if (mapping.Trigger != TriggerMoment.Tap)
            return false;
        if (string.IsNullOrWhiteSpace(mapping.HoldKeyboardKey))
            return false;
        if (mapping.HoldThresholdMs is < 0)
            return false;
        if (!ChordResolver.TryParseButtonChord(mapping.From?.Value, out _, out var reqRt, out var reqLt, out _))
            return false;
        if (reqRt || reqLt)
            return false;
        return true;
    }

    /// <summary>
    /// Per <see cref="MappingEntry.HoldThresholdMs"/> when set (&gt; 0); otherwise the app <c>modifierGraceMs</c> default.
    /// </summary>
    private int ResolveHoldThresholdMs(MappingEntry mapping)
    {
        var raw = mapping.HoldThresholdMs is > 0 ? mapping.HoldThresholdMs.Value : _modifierGraceMs;
        return Math.Clamp(raw, MinHoldThresholdMs, MaxHoldThresholdMs);
    }

    private static float GetTriggerMatchThreshold(MappingEntry mapping) =>
        mapping.AnalogThreshold is > 0 and <= 1 ? mapping.AnalogThreshold.Value : 0.35f;

    public bool TryArmHoldBinding(
        GamepadButtons buttons,
        IReadOnlyCollection<GamepadButtons> activeButtons,
        IReadOnlyList<MappingEntry> snapshot,
        float leftTriggerValue,
        float rightTriggerValue)
    {
        if (!_canDispatchOutput())
            return false;

        var comboLeads = _resolveComboLeads(snapshot);
        if (activeButtons.Any(b => b != buttons && comboLeads.Contains(b)))
        {
            // If a lead button is held, we don't arm a solo hold-dual session.
            // This prevents the short-release (Tap) from firing when the button is released
            // as part of a chord.
            return false;
        }

        var holdCandidates = new List<(MappingEntry Mapping, List<GamepadButtons> ChordButtons, bool ReqRt, bool ReqLt, string SourceToken)>();
        foreach (var mapping in snapshot)
        {
            if (mapping?.From is null || !IsHoldDualMapping(mapping)) continue;
            if (mapping.From.Type != GamepadBindingType.Button) continue;
            if (!ChordResolver.TryParseButtonChord(mapping.From.Value, out var chordButtons, out var reqRt, out var reqLt, out var sourceToken))
                continue;
            if (reqRt || reqLt)
                continue;
            var triggerThreshold = GetTriggerMatchThreshold(mapping);
            if (!ChordResolver.DoesChordMatchEvent(chordButtons, reqRt, reqLt, leftTriggerValue, rightTriggerValue, triggerThreshold, buttons, activeButtons))
                continue;
            holdCandidates.Add((mapping, chordButtons, reqRt, reqLt, sourceToken));
        }

        for (var i = 0; i < holdCandidates.Count; i++)
        {
            var candidate = holdCandidates[i];
            var hasMoreSpecific = holdCandidates.Any(other =>
                !ReferenceEquals(other.Mapping, candidate.Mapping) &&
                ChordResolver.IsOtherChordStrictlyMoreSpecific(
                    candidate.ChordButtons,
                    candidate.ReqRt,
                    candidate.ReqLt,
                    other.ChordButtons,
                    other.ReqRt,
                    other.ReqLt));
            if (hasMoreSpecific)
                continue;

            if (_holdSessions.ContainsKey(candidate.SourceToken))
                return true;

            if (!InputTokenResolver.TryResolveMappedOutput(candidate.Mapping.KeyboardKey, out _, out _) ||
                !InputTokenResolver.TryResolveMappedOutput(candidate.Mapping.HoldKeyboardKey, out _, out _))
                continue;

            var thresholdMs = ResolveHoldThresholdMs(candidate.Mapping);
            HoldSession session = null!;
            var timer = _timeProvider.CreateTimer(
                TimeSpan.FromMilliseconds(thresholdMs),
                () =>
                {
                    if (_runSynchronizedWithInputFrame is not null)
                        _runSynchronizedWithInputFrame(() => OnHoldTimerElapsed(session));
                    else
                        OnHoldTimerElapsed(session);
                });
            session = new HoldSession
            {
                SourceToken = candidate.SourceToken,
                ShortKeyToken = candidate.Mapping.KeyboardKey ?? string.Empty,
                HoldKeyToken = candidate.Mapping.HoldKeyboardKey ?? string.Empty,
                ChordButtons = candidate.ChordButtons,
                RequiresRightTrigger = candidate.ReqRt,
                RequiresLeftTrigger = candidate.ReqLt,
                TriggerMatchThreshold = GetTriggerMatchThreshold(candidate.Mapping),
                HoldThresholdMs = thresholdMs,
                Timer = timer
            };

            timer.Start();
            _holdSessions[candidate.SourceToken] = session;
            _setMappingStatus($"Hold armed: {candidate.SourceToken} ({thresholdMs} ms)");
            return true;
        }

        return false;
    }

    public bool TryArmRadialKeyboardConflict(
        GamepadButtons button,
        IReadOnlyCollection<GamepadButtons> activeButtons,
        IReadOnlyList<MappingEntry> snapshot,
        float leftTriggerValue,
        float rightTriggerValue)
    {
        if (_tryOpenRadialMenuFromKeyboardConflict is null || !_canDispatchOutput())
            return false;

        var comboLeads = _resolveComboLeads(snapshot);
        if (activeButtons.Any(b => b != button && comboLeads.Contains(b)))
            return false;

        if (!TryGetSoloKeyboardRadialConflict(button, snapshot, out var keyboardMapping, out var radialMapping))
            return false;

        if (_radialKeyboardConflictSessions.ContainsKey(button))
            return true;

        if (!ChordResolver.TryParseButtonChord(keyboardMapping.From!.Value, out var chordButtons, out var reqRt, out var reqLt, out var sourceToken))
            return false;
        if (reqRt || reqLt || chordButtons.Count != 1)
            return false;

        if (!InputTokenResolver.TryResolveMappedOutput(keyboardMapping.KeyboardKey, out _, out _))
            return false;

        var triggerThreshold = GetTriggerMatchThreshold(keyboardMapping);
        if (!ChordResolver.DoesChordMatchEvent(
                chordButtons,
                reqRt,
                reqLt,
                leftTriggerValue,
                rightTriggerValue,
                triggerThreshold,
                button,
                activeButtons))
            return false;

        var thresholdMs = ResolveHoldThresholdMs(keyboardMapping);
        RadialKeyboardConflictSession session = null!;
        var timer = _timeProvider.CreateTimer(
            TimeSpan.FromMilliseconds(thresholdMs),
            () =>
            {
                if (_runSynchronizedWithInputFrame is not null)
                    _runSynchronizedWithInputFrame(() => OnRadialKeyboardConflictTimerElapsed(session));
                else
                    OnRadialKeyboardConflictTimerElapsed(session);
            });
        session = new RadialKeyboardConflictSession
        {
            Button = button,
            SourceToken = sourceToken,
            ShortKeyToken = keyboardMapping.KeyboardKey ?? string.Empty,
            RadialMapping = radialMapping,
            Timer = timer
        };

        timer.Start();
        _radialKeyboardConflictSessions[button] = session;
        _setMappingStatus($"Radial vs tap armed: {sourceToken} ({thresholdMs} ms)");
        _onHoldSessionsChanged();
        return true;
    }

    private void OnRadialKeyboardConflictTimerElapsed(RadialKeyboardConflictSession session)
    {
        session.Timer.Stop();
        if (!_radialKeyboardConflictSessions.TryGetValue(session.Button, out var live) || !ReferenceEquals(live, session))
            return;

        if (!ChordPhysicallyActive(
                new[] { session.Button },
                false,
                false,
                _latestLeftTrigger,
                _latestRightTrigger,
                GetTriggerMatchThreshold(session.RadialMapping),
                _latestActiveButtons))
        {
            DisposeRadialKeyboardConflictSession(session.Button);
            return;
        }

        if (!_canDispatchOutput())
        {
            _setMappingStatus($"Suppressed radial hold ({session.SourceToken}) - target is not foreground");
            DisposeRadialKeyboardConflictSession(session.Button);
            return;
        }

        if (_tryOpenRadialMenuFromKeyboardConflict?.Invoke(session.RadialMapping, session.SourceToken) == true)
        {
            session.RadialOpened = true;
            _setMappingStatus($"Radial opened (hold): {session.SourceToken}");
        }
        else
            DisposeRadialKeyboardConflictSession(session.Button);
    }

    public bool TryFinalizeRadialKeyboardConflictOnRelease(
        GamepadButtons releasedButton,
        long? releasedButtonHeldMs,
        bool applyLeadReleaseSuppress)
    {
        if (_tryOpenRadialMenuFromKeyboardConflict is null ||
            !_radialKeyboardConflictSessions.TryGetValue(releasedButton, out var session))
            return false;

        session.Timer.Stop();

        if (session.RadialOpened)
        {
            DisposeRadialKeyboardConflictSession(releasedButton);
            return true;
        }

        if (!_canDispatchOutput())
        {
            DisposeRadialKeyboardConflictSession(releasedButton);
            return true;
        }

        if (!InputTokenResolver.TryResolveMappedOutput(session.ShortKeyToken, out var output, out var label))
        {
            DisposeRadialKeyboardConflictSession(releasedButton);
            return true;
        }

        var heldMs = releasedButtonHeldMs ?? TryGetPressedDurationMs(releasedButton);
        if (applyLeadReleaseSuppress &&
            heldMs.HasValue &&
            heldMs.Value > _leadKeyReleaseSuppressMs)
        {
            _setMappingStatus($"Suppressed tap ({session.SourceToken}) - lead held past {_leadKeyReleaseSuppressMs} ms");
            DisposeRadialKeyboardConflictSession(releasedButton);
            return true;
        }

        var outputLabel = $"{label} (Tap)";
        _setMappedOutput(outputLabel);
        _setMappingStatus($"Queued tap (radial conflict): {session.SourceToken} -> {outputLabel}");
        _queueOutputDispatch(session.SourceToken, TriggerMoment.Tap, output, outputLabel, session.ShortKeyToken);
        DisposeRadialKeyboardConflictSession(releasedButton);
        return true;
    }

    public void CancelRadialKeyboardConflictSupersededByMoreSpecificChord(
        GamepadButtons changedButton,
        IReadOnlyCollection<GamepadButtons> activeButtons,
        IReadOnlyList<MappingEntry> snapshot,
        float leftTriggerValue,
        float rightTriggerValue)
    {
        foreach (var kvp in _radialKeyboardConflictSessions.ToArray())
        {
            var session = kvp.Value;
            foreach (var mapping in snapshot)
            {
                if (mapping?.From is null || mapping.From.Type != GamepadBindingType.Button)
                    continue;
                if (!ChordResolver.TryParseButtonChord(mapping.From.Value, out var obChord, out var obRt, out var obLt, out _))
                    continue;
                if (!ChordResolver.IsOtherChordStrictlyMoreSpecific(
                        new[] { session.Button },
                        false,
                        false,
                        obChord,
                        obRt,
                        obLt))
                    continue;

                var th = GetTriggerMatchThreshold(mapping);
                if (!ChordResolver.DoesChordMatchEvent(
                        obChord,
                        obRt,
                        obLt,
                        leftTriggerValue,
                        rightTriggerValue,
                        th,
                        changedButton,
                        activeButtons))
                    continue;

                DisposeRadialKeyboardConflictSession(kvp.Key);
                break;
            }
        }
    }

    private void DisposeRadialKeyboardConflictSession(GamepadButtons button)
    {
        if (_radialKeyboardConflictSessions.Remove(button, out var session))
            session.Timer.Stop();
        _onHoldSessionsChanged();
    }

    public static bool HasSoloKeyboardRadialConflict(GamepadButtons button, IReadOnlyList<MappingEntry> snapshot) =>
        TryGetSoloKeyboardRadialConflict(button, snapshot, out _, out _);

    private static bool TryGetSoloKeyboardRadialConflict(
        GamepadButtons button,
        IReadOnlyList<MappingEntry> snapshot,
        out MappingEntry keyboardMapping,
        out MappingEntry radialMapping)
    {
        keyboardMapping = null!;
        radialMapping = null!;
        MappingEntry? kb = null;
        MappingEntry? rad = null;
        foreach (var mapping in snapshot)
        {
            if (mapping?.From is null || mapping.From.Type != GamepadBindingType.Button)
                continue;
            if (mapping.Trigger != TriggerMoment.Pressed)
                continue;
            if (!ChordResolver.TryParseButtonChord(mapping.From.Value, out var chord, out var rt, out var lt, out _))
                continue;
            if (rt || lt || chord.Count != 1 || chord[0] != button)
                continue;
            if (mapping.RadialMenu is not null)
                rad = mapping;
            else if (InputTokenResolver.TryResolveMappedOutput(mapping.KeyboardKey, out _, out _))
                kb = mapping;
        }

        if (kb is null || rad is null)
            return false;
        keyboardMapping = kb;
        radialMapping = rad;
        return true;
    }

    private void OnHoldTimerElapsed(HoldSession session)
    {
        session.Timer.Stop();
        if (!_holdSessions.TryGetValue(session.SourceToken, out var live) || !ReferenceEquals(live, session))
            return;

        if (!ChordPhysicallyActive(
                session.ChordButtons,
                session.RequiresRightTrigger,
                session.RequiresLeftTrigger,
                _latestLeftTrigger,
                _latestRightTrigger,
                session.TriggerMatchThreshold,
                _latestActiveButtons))
        {
            DisposeHoldSession(session.SourceToken);
            return;
        }

        if (!_canDispatchOutput())
        {
            _setMappingStatus($"Suppressed hold output ({session.SourceToken}) - target is not foreground");
            DisposeHoldSession(session.SourceToken);
            return;
        }

        if (!InputTokenResolver.TryResolveMappedOutput(session.HoldKeyToken, out var output, out var label))
        {
            DisposeHoldSession(session.SourceToken);
            return;
        }

        session.LongFired = true;
        var outputLabel = $"{label} (Hold)";
        _setMappedOutput(outputLabel);
        _setMappingStatus($"Queued hold: {session.SourceToken} -> {outputLabel}");
        _queueOutputDispatch(session.SourceToken, TriggerMoment.Tap, output, outputLabel, session.HoldKeyToken);
    }

    public void HandleHoldBindingRelease(
        GamepadButtons releasedButton,
        IReadOnlyCollection<GamepadButtons> activeButtonsPreRelease,
        float leftTriggerValue,
        float rightTriggerValue,
        long? releasedButtonHeldMs,
        bool applyLeadReleaseSuppress)
    {
        var postRelease = new HashSet<GamepadButtons>(activeButtonsPreRelease);
        postRelease.Remove(releasedButton);

        var tokensToRemove = new List<string>();
        foreach (var kvp in _holdSessions)
        {
            var session = kvp.Value;
            if (!session.ChordButtons.Contains(releasedButton))
                continue;

            session.Timer.Stop();
            if (!ChordPhysicallyActive(
                    session.ChordButtons,
                    session.RequiresRightTrigger,
                    session.RequiresLeftTrigger,
                    leftTriggerValue,
                    rightTriggerValue,
                    session.TriggerMatchThreshold,
                    postRelease))
            {
                if (!session.LongFired && _canDispatchOutput())
                {
                    var heldMs = releasedButtonHeldMs ?? TryGetPressedDurationMs(releasedButton);
                    if (applyLeadReleaseSuppress &&
                        heldMs.HasValue && heldMs.Value > _leadKeyReleaseSuppressMs)
                        _setMappingStatus($"Suppressed tap ({session.SourceToken}) - lead held past {_leadKeyReleaseSuppressMs} ms");
                    else if (InputTokenResolver.TryResolveMappedOutput(session.ShortKeyToken, out var output, out var label))
                    {
                        var outputLabel = $"{label} (Tap)";
                        _setMappedOutput(outputLabel);
                        _setMappingStatus($"Queued tap: {session.SourceToken} -> {outputLabel}");
                        _queueOutputDispatch(session.SourceToken, TriggerMoment.Tap, output, outputLabel, session.ShortKeyToken);
                    }
                }

                tokensToRemove.Add(kvp.Key);
            }
        }

        foreach (var t in tokensToRemove)
            DisposeHoldSession(t);
    }

    private static bool ChordPhysicallyActive(
        IReadOnlyCollection<GamepadButtons> chordButtons,
        bool requiresRightTrigger,
        bool requiresLeftTrigger,
        float leftTriggerValue,
        float rightTriggerValue,
        float triggerMatchThreshold,
        IReadOnlyCollection<GamepadButtons> activeButtons)
    {
        foreach (var button in chordButtons)
        {
            if (!activeButtons.Contains(button))
                return false;
        }

        if (requiresRightTrigger && rightTriggerValue < triggerMatchThreshold)
            return false;
        if (requiresLeftTrigger && leftTriggerValue < triggerMatchThreshold)
            return false;
        return true;
    }

    public void CancelHoldSessionsSupersededByMoreSpecificChord(
        GamepadButtons changedButton,
        IReadOnlyCollection<GamepadButtons> activeButtons,
        IReadOnlyList<MappingEntry> snapshot,
        float leftTriggerValue,
        float rightTriggerValue)
    {
        foreach (var kvp in _holdSessions.ToArray())
        {
            var session = kvp.Value;
            if (session.ChordButtons.Count != 1)
                continue;
            foreach (var mapping in snapshot)
            {
                if (mapping?.From is null || mapping.From.Type != GamepadBindingType.Button)
                    continue;
                if (!ChordResolver.TryParseButtonChord(mapping.From.Value, out var obChord, out var obRt, out var obLt, out _))
                    continue;
                if (!ChordResolver.IsOtherChordStrictlyMoreSpecific(
                        session.ChordButtons,
                        session.RequiresRightTrigger,
                        session.RequiresLeftTrigger,
                        obChord,
                        obRt,
                        obLt))
                    continue;

                var th = GetTriggerMatchThreshold(mapping);
                if (!ChordResolver.DoesChordMatchEvent(
                        obChord,
                        obRt,
                        obLt,
                        leftTriggerValue,
                        rightTriggerValue,
                        th,
                        changedButton,
                        activeButtons))
                    continue;

                DisposeHoldSession(kvp.Key);
                break;
            }
        }
    }

    private void DisposeHoldSession(string sourceToken)
    {
        if (_holdSessions.Remove(sourceToken, out var session))
            session.Timer.Stop();
        _onHoldSessionsChanged();
    }

    public void ClearAllHoldSessions()
    {
        foreach (var session in _holdSessions.Values)
            session.Timer.Stop();
        _holdSessions.Clear();
        foreach (var s in _radialKeyboardConflictSessions.Values)
            s.Timer.Stop();
        _radialKeyboardConflictSessions.Clear();
        _onHoldSessionsChanged();
    }
}
