using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Interfaces.Services;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Core;

internal sealed class RadialMenuController : IRadialMenuController
{
    private readonly IRadialMenuHud _radialMenuHud;
    private readonly Action<Action> _runOnUi;
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
                _runOnUi(() => _radialMenuHud.UpdateSelection(value));
            }
        }
    }

    private readonly Action<string>? _requestTemplateSwitch;
    private IKeyboardActionCatalog? _catalog;

    public RadialMenuController(
        IRadialMenuHud radialMenuHud,
        Action<Action> runOnUi,
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
        _runOnUi = runOnUi;
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
                    // Use Atan2(x, y) so 0 is UP and it increases clockwise
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

            if (shouldClose)
            {
                CloseInternal(confirmOnClose, confirmOnClose);
                return;
            }
        }

        // Call the UI on every frame where stick is engaged (or just released) 
        // to ensure the highlight stays in sync with the physical stick.
        if (indexToNotify.HasValue)
        {
            _runOnUi(() => _radialMenuHud.UpdateSelection(indexToNotify.Value));
        }
    }

    public void HandleButtonReleased(GamepadButtons releasedButton)
    {
        lock (_stateLock)
        {
            if (_activeRadial is null || _openMapping is null)
                return;

            if (_activeChord.Contains(releasedButton))
            {
                var confirmOnRelease = _getConfirmMode() != RadialMenuConfirmMode.ReturnStickToCenter;
                CloseInternal(confirmOnRelease, confirmOnRelease);
            }
        }
    }

    public void ForceCancel()
    {
        lock (_stateLock)
        {
            ForceReset();
        }
    }

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

            var items = definition.Items.Select(item =>
            {
                var action = _catalog?.GetAction(item.ActionId)
                             ?? _keyboardActions?.FirstOrDefault(a =>
                                 string.Equals(a.Id, item.ActionId, StringComparison.OrdinalIgnoreCase));
                var hudLine = (item.Label ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(hudLine))
                {
                    hudLine = (action?.Description ?? string.Empty).Trim();
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

            _runOnUi(() => _radialMenuHud.ShowMenu(definition.DisplayName, items));
            _registerActiveAction(this);
            return true;
        }
    }

    public bool TryClose(string radialMenuId, string sourceToken, bool dispatchSelection, bool suppressChord = false)
    {
        lock (_stateLock)
        {
            if (_activeRadial is null)
                return false;

            if (!string.Equals(radialMenuId, _activeRadial.Id, StringComparison.Ordinal))
                return false;

            CloseInternal(dispatchSelection, suppressChord);
            return true;
        }
    }

    public void ForceReset()
    {
        lock (_stateLock)
        {
            _activeChord.Clear();
            if (_activeRadial is null)
                return;

            _runOnUi(() => _radialMenuHud.HideMenu());
            var id = _activeRadial.Id;
            ClearState();
            _unregisterActiveAction(id);
        }
    }

    private void CloseInternal(bool dispatchSelection, bool suppressChord)
    {
        if (_activeRadial is null) return;

        var selectedIndex = _selectedIndex;
        var definition = _activeRadial;
        var sourceToken = string.IsNullOrEmpty(_sourceToken) ? "RadialMenu" : _sourceToken;
        var id = definition.Id;

        _runOnUi(() => _radialMenuHud.HideMenu());

        if (dispatchSelection && selectedIndex >= 0 && selectedIndex < definition.Items.Count)
        {
            var item = definition.Items[selectedIndex];
            var action = _catalog?.GetAction(item.ActionId)
                         ?? _keyboardActions?.FirstOrDefault(a =>
                             string.Equals(a.Id, item.ActionId, StringComparison.OrdinalIgnoreCase));

            if (action != null && _actionExecutor != null)
            {
                _actionExecutor.Execute(action, sourceToken, out var err);
                if (!string.IsNullOrEmpty(err))
                    _setMappingStatus(err);
            }
        }

        if (!suppressChord)
        {
            _activeChord.Clear();
        }

        _unregisterActiveAction(id);
        ClearState();
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
