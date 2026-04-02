using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using GamepadMapperGUI.Models;
using Vortice.XInput;

namespace GamepadMapperGUI.Core;

internal sealed class ComboHudManager : IDisposable
{
    private readonly Action<ComboHudContent?> _setComboHud;
    private readonly Func<bool> _canDispatchOutput;
    private readonly Func<IReadOnlyList<MappingEntry>> _getMappingsSnapshot;
    private readonly Func<IReadOnlyCollection<GamepadButtons>> _getLatestActiveButtons;
    private readonly Func<IReadOnlyCollection<MappingEntry>, HashSet<GamepadButtons>> _resolveComboLeads;
    private readonly HoldSessionManager _holdSessionManager;
    private readonly int _comboHudDelayMs;
    private readonly Action<string?>? _setComboHudGateHint;
    private readonly Func<string>? _comboHudGateMessageFactory;
    private readonly Func<bool>? _isComboHudPresentationSuppressed;

    private DispatcherTimer? _comboHudDelayTimer;
    private bool _comboHudDelayConfirmed;
    private string? _pendingComboHudSignature;
    private long _comboHudArmTickCount64;
    private string? _lastPresentedSignature;
    private string? _lastEmittedGateHint;

    public ComboHudManager(
        Action<ComboHudContent?> setComboHud,
        Func<bool> canDispatchOutput,
        Func<IReadOnlyList<MappingEntry>> getMappingsSnapshot,
        Func<IReadOnlyCollection<GamepadButtons>> getLatestActiveButtons,
        Func<IReadOnlyCollection<MappingEntry>, HashSet<GamepadButtons>> resolveComboLeads,
        HoldSessionManager holdSessionManager,
        int comboHudDelayMs,
        Action<string?>? setComboHudGateHint = null,
        Func<string>? comboHudGateMessageFactory = null,
        Func<bool>? isComboHudPresentationSuppressed = null)
    {
        _setComboHud = setComboHud;
        _canDispatchOutput = canDispatchOutput;
        _getMappingsSnapshot = getMappingsSnapshot;
        _getLatestActiveButtons = getLatestActiveButtons;
        _resolveComboLeads = resolveComboLeads;
        _holdSessionManager = holdSessionManager;
        _comboHudDelayMs = comboHudDelayMs;
        _setComboHudGateHint = setComboHudGateHint;
        _comboHudGateMessageFactory = comboHudGateMessageFactory;
        _isComboHudPresentationSuppressed = isComboHudPresentationSuppressed;
    }

    internal void InvalidateLastPresentedSignature() => _lastPresentedSignature = null;

    internal bool AwaitingComboHudDelay =>
        !string.IsNullOrEmpty(_pendingComboHudSignature) && !_comboHudDelayConfirmed;

    public void Sync()
    {
        try
        {
            if (!TryGetComboHudSignature(out var signature))
            {
                CancelComboHudDelayTimer();
                _comboHudDelayConfirmed = false;
                _pendingComboHudSignature = null;
                _lastPresentedSignature = null;
                _setComboHud(null);
                return;
            }

            if (!string.Equals(signature, _pendingComboHudSignature, StringComparison.Ordinal))
            {
                _pendingComboHudSignature = signature;
                _comboHudDelayConfirmed = false;
                _comboHudArmTickCount64 = Environment.TickCount64;
                CancelComboHudDelayTimer();
                StartComboHudDelayTimer();
            }

            if (!_comboHudDelayConfirmed)
            {
                var elapsedMs = Environment.TickCount64 - _comboHudArmTickCount64;
                if (elapsedMs >= _comboHudDelayMs)
                {
                    CancelComboHudDelayTimer();
                    _comboHudDelayConfirmed = true;
                }
                else
                {
                    EnsureComboHudDelayTimer();
                    if (_comboHudDelayTimer is { IsEnabled: false })
                        StartComboHudDelayTimer();
                    return;
                }
            }

            PresentComboHudForCurrentSignature(signature);
        }
        finally
        {
            UpdateComboHudGateHint();
        }
    }

    private void UpdateComboHudGateHint()
    {
        if (_setComboHudGateHint is null)
            return;

        if (_holdSessionManager.TryGetFirstHoldSession(out var hs) && hs is not null)
        {
            EmitGateHintIfChanged(null);
            return;
        }

        var mappings = _getMappingsSnapshot();
        var comboLeads = _resolveComboLeads(mappings);
        var activeButtons = _getLatestActiveButtons();

        if (!ComboHudBuilder.HasModifierPrefixHudContent(activeButtons, mappings, comboLeads))
        {
            EmitGateHintIfChanged(null);
            return;
        }

        if (!_canDispatchOutput())
        {
            var msg = _comboHudGateMessageFactory?.Invoke();
            if (string.IsNullOrEmpty(msg))
                msg = "Combo HUD disabled: output is not allowed.";
            EmitGateHintIfChanged(msg);
        }
        else
            EmitGateHintIfChanged(null);
    }

    private void EmitGateHintIfChanged(string? hint)
    {
        if (_setComboHudGateHint is null)
            return;

        if (string.Equals(_lastEmittedGateHint, hint, StringComparison.Ordinal))
            return;

        _lastEmittedGateHint = hint;
        _setComboHudGateHint.Invoke(hint);
    }

    private bool TryGetComboHudSignature(out string signature)
    {
        if (_holdSessionManager.TryGetFirstHoldSession(out var holdSession) && holdSession is not null)
        {
            signature = $"hold|{holdSession.SourceToken}";
            return true;
        }

        var mappings = _getMappingsSnapshot();
        var comboLeads = _resolveComboLeads(mappings);
        var activeButtons = _getLatestActiveButtons();

        var prefix = ComboHudBuilder.BuildModifierPrefixHud(_canDispatchOutput, activeButtons, mappings, comboLeads);
        if (prefix is null)
        {
            signature = string.Empty;
            return false;
        }

        var thumbprint = string.Join(
            ',',
            activeButtons.OrderBy(b => b.ToString(), StringComparer.OrdinalIgnoreCase));
        signature = $"prefix|{thumbprint}";
        return true;
    }

    private void PresentComboHudForCurrentSignature(string signature)
    {
        if (_isComboHudPresentationSuppressed?.Invoke() == true)
            return;

        if (_comboHudDelayConfirmed &&
            string.Equals(signature, _lastPresentedSignature, StringComparison.Ordinal))
            return;

        var mappings = _getMappingsSnapshot();
        var comboLeads = _resolveComboLeads(mappings);

        if (signature.StartsWith("hold|", StringComparison.Ordinal) &&
            _holdSessionManager.TryGetFirstHoldSession(out var holdSession) &&
            holdSession is not null)
        {
            _lastPresentedSignature = signature;
            _setComboHud(ComboHudBuilder.BuildComboHud(holdSession, mappings, comboLeads));
            return;
        }

        var activeButtons = _getLatestActiveButtons();
        var prefix = ComboHudBuilder.BuildModifierPrefixHud(_canDispatchOutput, activeButtons, mappings, comboLeads);
        if (prefix is not null)
        {
            _lastPresentedSignature = signature;
            _setComboHud(prefix);
        }
        else
        {
            _lastPresentedSignature = null;
            _setComboHud(null);
        }
    }

    private void StartComboHudDelayTimer()
    {
        EnsureComboHudDelayTimer();
        _comboHudDelayTimer!.Stop();
        _comboHudDelayTimer.Interval = TimeSpan.FromMilliseconds(_comboHudDelayMs);
        _comboHudDelayTimer.Start();
    }

    private void CancelComboHudDelayTimer()
    {
        _comboHudDelayTimer?.Stop();
    }

    private void EnsureComboHudDelayTimer()
    {
        if (_comboHudDelayTimer is not null)
            return;

        var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        _comboHudDelayTimer = new DispatcherTimer(DispatcherPriority.Input, dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(_comboHudDelayMs)
        };
        _comboHudDelayTimer.Tick += OnComboHudDelayTimerTick;
    }

    private void OnComboHudDelayTimerTick(object? sender, EventArgs e)
    {
        try
        {
            _comboHudDelayTimer?.Stop();

            if (_comboHudDelayConfirmed)
                return;

            if (!TryGetComboHudSignature(out var signature))
            {
                _comboHudDelayConfirmed = false;
                _pendingComboHudSignature = null;
                _lastPresentedSignature = null;
                _setComboHud(null);
                return;
            }

            if (!string.Equals(signature, _pendingComboHudSignature, StringComparison.Ordinal))
                return;

            _comboHudDelayConfirmed = true;
            PresentComboHudForCurrentSignature(signature);
        }
        finally
        {
            UpdateComboHudGateHint();
        }
    }

    public void Dispose()
    {
        EmitGateHintIfChanged(null);

        if (_comboHudDelayTimer is null)
            return;

        _comboHudDelayTimer.Stop();
        _comboHudDelayTimer.Tick -= OnComboHudDelayTimerTick;
        _comboHudDelayTimer = null;
    }
}
