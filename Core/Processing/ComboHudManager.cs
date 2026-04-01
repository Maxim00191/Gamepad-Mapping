using System;
using System.Collections.Generic;
using System.Linq;
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

    private DispatcherTimer? _comboHudDelayTimer;
    private bool _comboHudDelayConfirmed;
    private string? _pendingComboHudSignature;

    public ComboHudManager(
        Action<ComboHudContent?> setComboHud,
        Func<bool> canDispatchOutput,
        Func<IReadOnlyList<MappingEntry>> getMappingsSnapshot,
        Func<IReadOnlyCollection<GamepadButtons>> getLatestActiveButtons,
        Func<IReadOnlyCollection<MappingEntry>, HashSet<GamepadButtons>> resolveComboLeads,
        HoldSessionManager holdSessionManager,
        int comboHudDelayMs)
    {
        _setComboHud = setComboHud;
        _canDispatchOutput = canDispatchOutput;
        _getMappingsSnapshot = getMappingsSnapshot;
        _getLatestActiveButtons = getLatestActiveButtons;
        _resolveComboLeads = resolveComboLeads;
        _holdSessionManager = holdSessionManager;
        _comboHudDelayMs = comboHudDelayMs;
    }

    public void Sync()
    {
        if (!TryGetComboHudSignature(out var signature))
        {
            CancelComboHudDelayTimer();
            _comboHudDelayConfirmed = false;
            _pendingComboHudSignature = null;
            _setComboHud(null);
            return;
        }

        if (!string.Equals(signature, _pendingComboHudSignature, StringComparison.Ordinal))
        {
            _pendingComboHudSignature = signature;
            _comboHudDelayConfirmed = false;
            CancelComboHudDelayTimer();
        }

        if (!_comboHudDelayConfirmed)
        {
            ScheduleComboHudDelay();
            return;
        }

        CancelComboHudDelayTimer();
        PresentComboHudForCurrentSignature(signature);
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
        var mappings = _getMappingsSnapshot();
        var comboLeads = _resolveComboLeads(mappings);

        if (signature.StartsWith("hold|", StringComparison.Ordinal) &&
            _holdSessionManager.TryGetFirstHoldSession(out var holdSession) &&
            holdSession is not null)
        {
            _setComboHud(ComboHudBuilder.BuildComboHud(holdSession, mappings, comboLeads));
            return;
        }

        var activeButtons = _getLatestActiveButtons();
        var prefix = ComboHudBuilder.BuildModifierPrefixHud(_canDispatchOutput, activeButtons, mappings, comboLeads);
        if (prefix is not null)
            _setComboHud(prefix);
        else
            _setComboHud(null);
    }

    private void ScheduleComboHudDelay()
    {
        EnsureComboHudDelayTimer();
        _comboHudDelayTimer!.Stop();
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

        _comboHudDelayTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(_comboHudDelayMs) };
        _comboHudDelayTimer.Tick += OnComboHudDelayTimerTick;
    }

    private void OnComboHudDelayTimerTick(object? sender, EventArgs e)
    {
        _comboHudDelayTimer?.Stop();

        if (!TryGetComboHudSignature(out var signature))
        {
            _comboHudDelayConfirmed = false;
            _pendingComboHudSignature = null;
            _setComboHud(null);
            return;
        }

        _comboHudDelayConfirmed = true;
        PresentComboHudForCurrentSignature(signature);
    }

    public void Dispose()
    {
        if (_comboHudDelayTimer is not null)
        {
            _comboHudDelayTimer.Stop();
            _comboHudDelayTimer.Tick -= OnComboHudDelayTimerTick;
            _comboHudDelayTimer = null;
        }
    }
}
