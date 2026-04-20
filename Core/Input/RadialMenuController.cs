using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Storage;
using GamepadMapperGUI.Interfaces.Services.Update;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Interfaces.Services.Radial;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Core;

internal sealed class RadialMenuController : IRadialMenuController
{
    private readonly IRadialMenuHud _radialMenuHud;
    private readonly IUiSynchronization _ui;
    private readonly Action<string> _setMappedOutput;
    private readonly Action<string> _setMappingStatus;
    private readonly Action<string, TriggerMoment, DispatchedOutput, string, string> _enqueueOutput;
    private readonly Func<RadialMenuConfirmMode> _getConfirmMode;
    private readonly Action<IActiveAction> _registerActiveAction;
    private readonly Action<string> _unregisterActiveAction;

    private List<RadialMenuDefinition>? _radialMenus;
    private List<KeyboardActionDefinition>? _keyboardActions;
    private IKeyboardActionExecutor? _actionExecutor;

    private RadialMenuDefinition? _activeRadial;
    private MappingEntry? _openMapping;
    private string _sourceToken = string.Empty;
    private int _selectedIndex = -1;
    private HashSet<GamepadButtons> _activeChord = [];

    private bool _prevStickEngaged;
    private bool _stickEverEngagedWhileOpen;
    private int _lastSectorWhileEngaged = -1;

    private readonly object _stateLock = new();

    public string Id
    {
        get
        {
            lock (_stateLock)
                return _activeRadial?.Id ?? string.Empty;
        }
    }

    public IReadOnlySet<GamepadButtons> ActiveChord
    {
        get
        {
            lock (_stateLock)
                return _activeChord;
        }
    }

    public RadialMenuDefinition? ActiveRadial
    {
        get
        {
            lock (_stateLock)
                return _activeRadial;
        }
    }

    public int CurrentSelectedIndex
    {
        get
        {
            lock (_stateLock)
                return _selectedIndex;
        }
        set
        {
            bool changed = false;
            lock (_stateLock)
            {
                if (_selectedIndex != value)
                {
                    _selectedIndex = value;
                    changed = true;
                }
            }
            if (changed)
            {
                _ui.Post(() => _radialMenuHud.UpdateSelection(value));
            }
        }
    }

    private readonly Action<string>? _requestTemplateSwitch;
    private IKeyboardActionCatalog? _catalog;

    public RadialMenuController(
        IRadialMenuHud radialMenuHud,
        IUiSynchronization ui,
        Action<string> setMappedOutput,
        Action<string> setMappingStatus,
        Action<string, TriggerMoment, DispatchedOutput, string, string> enqueueOutput,
        Func<RadialMenuConfirmMode> getConfirmMode,
        Action<IActiveAction> registerActiveAction,
        Action<string> unregisterActiveAction,
        Action<string>? requestTemplateSwitch = null,
        IKeyboardActionCatalog? catalog = null)
    {
        _radialMenuHud = radialMenuHud;
        _ui = ui;
        _setMappedOutput = setMappedOutput;
        _setMappingStatus = setMappingStatus;
        _enqueueOutput = enqueueOutput;
        _getConfirmMode = getConfirmMode;
        _registerActiveAction = registerActiveAction;
        _unregisterActiveAction = unregisterActiveAction;
        _requestTemplateSwitch = requestTemplateSwitch;
        _catalog = catalog;
    }

    public void UpdateSelection(Vector2 stick, float engagementThreshold, RadialMenuConfirmMode confirmMode)
    {
        int? indexToNotify = null;
        bool shouldClose = false;
        bool confirmOnClose = false;

        lock (_stateLock)
        {
            if (_activeRadial == null) return;

            var minMag = Math.Clamp(engagementThreshold, 0.01f, 1f);
            var engaged = stick.Length() >= minMag;

            if (engaged)
            {
                _stickEverEngagedWhileOpen = true;
                var itemCount = _activeRadial.Items.Count;
                if (itemCount > 0)
                {
                    var angleRad = Math.Atan2(stick.X, stick.Y);
                    var angleDeg = angleRad * (180.0 / Math.PI);
                    if (angleDeg < 0) angleDeg += 360;

                    var sectorSize = 360.0 / itemCount;
                    var index = (int)((angleDeg + (sectorSize / 2)) / sectorSize) % itemCount;

                    _selectedIndex = index;
                    _lastSectorWhileEngaged = index;
                    indexToNotify = index;
                }
            }
            else
            {
                if (confirmMode == RadialMenuConfirmMode.ReturnStickToCenter &&
                    _prevStickEngaged &&
                    _stickEverEngagedWhileOpen &&
                    _lastSectorWhileEngaged >= 0)
                {
                    _selectedIndex = _lastSectorWhileEngaged;
                    shouldClose = true;
                    confirmOnClose = true;
                }
                else if (_selectedIndex != -1)
                {
                    _selectedIndex = -1;
                    indexToNotify = -1;
                }
            }

            _prevStickEngaged = engaged;
        }

        if (shouldClose)
        {
            CloseInternal(confirmOnClose, confirmOnClose);
            return;
        }

        if (indexToNotify.HasValue)
        {
            _ui.Post(() => _radialMenuHud.UpdateSelection(indexToNotify.Value));
        }
    }

    public void HandleButtonReleased(GamepadButtons releasedButton)
    {
        bool shouldClose = false;
        bool confirmOnRelease = false;

        lock (_stateLock)
        {
            if (_activeRadial is null || _openMapping is null)
                return;

            if (_activeChord.Contains(releasedButton))
            {
                confirmOnRelease = _getConfirmMode() != RadialMenuConfirmMode.ReturnStickToCenter;
                shouldClose = true;
            }
        }

        if (shouldClose)
            CloseInternal(confirmOnRelease, confirmOnRelease);
    }

    public void ForceCancel() => ForceReset();

    public void SetDefinitions(List<RadialMenuDefinition>? radialMenus, List<KeyboardActionDefinition>? keyboardActions, IKeyboardActionCatalog? catalog = null)
    {
        lock (_stateLock)
        {
            _radialMenus = radialMenus;
            _keyboardActions = keyboardActions;
            _catalog = catalog;
        }
    }

    public void SetActionExecutor(IKeyboardActionExecutor executor)
    {
        lock (_stateLock)
        {
            _actionExecutor = executor;
        }
    }

    public bool TryOpen(MappingEntry mapping, string sourceToken, out string? errorStatus)
    {
        string displayName;
        List<RadialMenuHudItem> items;

        lock (_stateLock)
        {
            errorStatus = null;
            if (mapping.RadialMenu is not { } rm)
                return false;

            var definition = _radialMenus?.FirstOrDefault(d => d.Id == rm.RadialMenuId);
            if (definition == null)
            {
                errorStatus = "Radial menu: unknown id.";
                return true;
            }

            _openMapping = mapping;
            _sourceToken = sourceToken;
            _activeRadial = definition;
            _selectedIndex = -1;

            if (mapping.From is { } from && from.Type == GamepadBindingType.Button &&
                ChordResolver.TryParseButtonChord(from.Value, out var chordButtons, out _, out _, out _))
            {
                _activeChord = new HashSet<GamepadButtons>(chordButtons);
            }
            else
            {
                _activeChord = [];
            }

            _prevStickEngaged = false;
            _stickEverEngagedWhileOpen = false;
            _lastSectorWhileEngaged = -1;

            items = definition.Items.Select(item =>
            {
                var action = _catalog?.GetAction(item.ActionId)
                             ?? _keyboardActions?.FirstOrDefault(a =>
                                 string.Equals(a.Id, item.ActionId, StringComparison.OrdinalIgnoreCase));
                var hudLine = (item.ResolvedLabel ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(hudLine))
                    hudLine = (item.Label ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(hudLine))
                {
                    hudLine = (action?.ResolvedCatalogDescription ?? action?.Description ?? string.Empty).Trim();
                    if (string.IsNullOrEmpty(hudLine))
                        hudLine = item.ActionId;
                }

                var keyLabel = (action?.KeyboardKey ?? string.Empty).Trim();
                return new RadialMenuHudItem
                {
                    ActionId = item.ActionId,
                    DisplayName = hudLine,
                    KeyboardKeyLabel = keyLabel,
                    Icon = item.Icon
                };
            }).ToList();

            displayName = !string.IsNullOrWhiteSpace(definition.ResolvedDisplayName)
                ? definition.ResolvedDisplayName.Trim()
                : (definition.DisplayName ?? string.Empty).Trim();
            _registerActiveAction(this);
        }

        _ui.Post(() => _radialMenuHud.ShowMenu(displayName, items));
        return true;
    }

    public bool TryClose(string radialMenuId, string sourceToken, bool dispatchSelection, bool suppressChord = false)
    {
        lock (_stateLock)
        {
            if (_activeRadial is null)
                return false;

            if (!string.Equals(radialMenuId, _activeRadial.Id, StringComparison.Ordinal))
                return false;
        }

        CloseInternal(dispatchSelection, suppressChord);
        return true;
    }

    public void ForceReset()
    {
        lock (_stateLock)
        {
            _activeChord.Clear();
            if (_activeRadial is null)
                return;

            var id = _activeRadial.Id;
            ClearState();
            if (id.Length > 0)
                _unregisterActiveAction(id);
        }

        _ui.Send(() => _radialMenuHud.HideMenu());
    }

    private readonly struct RadialCloseSnapshot
    {
        public required string RadialId { get; init; }
        public required string SourceToken { get; init; }
        public required bool DispatchSelection { get; init; }
        public required bool SuppressChord { get; init; }
        public KeyboardActionDefinition? Action { get; init; }
        public IKeyboardActionExecutor? Executor { get; init; }
    }

    private void CloseInternal(bool dispatchSelection, bool suppressChord)
    {
        RadialCloseSnapshot snap;

        lock (_stateLock)
        {
            if (_activeRadial is null) return;

            var definition = _activeRadial;
            var radialId = definition.Id;
            var sourceToken = string.IsNullOrEmpty(_sourceToken) ? "RadialMenu" : _sourceToken;
            var selectedIndex = _selectedIndex;
            var executor = _actionExecutor;

            KeyboardActionDefinition? action = null;
            if (dispatchSelection && selectedIndex >= 0 && selectedIndex < definition.Items.Count)
            {
                var item = definition.Items[selectedIndex];
                action = _catalog?.GetAction(item.ActionId)
                         ?? _keyboardActions?.FirstOrDefault(a =>
                             string.Equals(a.Id, item.ActionId, StringComparison.OrdinalIgnoreCase));
            }

            snap = new RadialCloseSnapshot
            {
                RadialId = radialId,
                SourceToken = sourceToken,
                DispatchSelection = dispatchSelection,
                SuppressChord = suppressChord,
                Action = action,
                Executor = executor
            };
        }

        _ui.Send(() => _radialMenuHud.HideMenu());

        if (snap.DispatchSelection && snap.Action is { } a && snap.Executor is { } ex)
        {
            ex.Execute(a, snap.SourceToken, out var err);
            if (!string.IsNullOrEmpty(err))
                _setMappingStatus(err);
        }

        lock (_stateLock)
        {
            if (_activeRadial is null || !string.Equals(_activeRadial.Id, snap.RadialId, StringComparison.Ordinal))
                return;

            if (!snap.SuppressChord)
                _activeChord.Clear();

            _unregisterActiveAction(snap.RadialId);
            ClearState();
        }
    }

    private void ClearState()
    {
        _activeRadial = null;
        _openMapping = null;
        _sourceToken = string.Empty;
        _selectedIndex = -1;
        _prevStickEngaged = false;
        _stickEverEngagedWhileOpen = false;
        _lastSectorWhileEngaged = -1;
    }
}
