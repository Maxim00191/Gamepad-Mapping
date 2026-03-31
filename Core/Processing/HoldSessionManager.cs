using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;
using GamepadMapperGUI.Models;
using Vortice.XInput;

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
    private readonly int _modifierGraceMs;
    private readonly int _leadKeyReleaseSuppressMs;

    private readonly Dictionary<string, HoldSession> _holdSessions = new(StringComparer.Ordinal);
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
        int modifierGraceMs,
        int leadKeyReleaseSuppressMs)
    {
        _canDispatchOutput = canDispatchOutput;
        _setMappedOutput = setMappedOutput;
        _setMappingStatus = setMappingStatus;
        _queueOutputDispatch = queueOutputDispatch;
        _onHoldSessionsChanged = onHoldSessionsChanged;
        _modifierGraceMs = modifierGraceMs;
        _leadKeyReleaseSuppressMs = leadKeyReleaseSuppressMs;
    }

    /// <summary>
    /// Elapsed ms since <see cref="RegisterButtonPressed"/> for <paramref name="button"/>, or <c>null</c> if not tracked.
    /// </summary>
    public long? TryGetPressedDurationMs(GamepadButtons button)
    {
        if (!_buttonDownTicks.TryGetValue(button, out var downTick))
            return null;
        var now = Environment.TickCount64;
        var delta = (long)((ulong)((ulong)now - (ulong)downTick));
        return delta;
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
        public required DispatcherTimer Timer { get; init; }
        public bool LongFired { get; set; }
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
    public void RegisterButtonPressed(GamepadButtons button) =>
        _buttonDownTicks[button] = Environment.TickCount64;

    public void RegisterButtonReleased(GamepadButtons button) =>
        _buttonDownTicks.Remove(button);

    public void ClearButtonDownTicks() => _buttonDownTicks.Clear();

    /// <summary>
    /// Blocks chord Tap/Pressed when another chord member was already held longer than the configured modifier grace window.
    /// </summary>
    public bool ShouldSuppressChordForOverdueModifier(IReadOnlyList<GamepadButtons> chordButtons, GamepadButtons changedButton)
    {
        if (chordButtons.Count < 2)
            return false;

        var now = Environment.TickCount64;
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

        return false;
    }

    public static bool IsHoldDualMapping(MappingEntry mapping)
    {
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
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(thresholdMs) };
            var session = new HoldSession
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

            timer.Tick += (_, _) => OnHoldTimerElapsed(session);
            timer.Start();
            _holdSessions[candidate.SourceToken] = session;
            _setMappingStatus($"Hold armed: {candidate.SourceToken} ({thresholdMs} ms)");
            return true;
        }

        return false;
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
        _onHoldSessionsChanged();
    }
}
