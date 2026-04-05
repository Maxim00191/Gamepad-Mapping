using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Interfaces.Services;
using GamepadMapperGUI.Models;
using Vortice.XInput;

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

    private RadialMenuDefinition? _activeRadial;
    private MappingEntry? _openMapping;
    private string _sourceToken = string.Empty;
    private int _selectedIndex = -1;
    private HashSet<GamepadButtons> _activeChord = [];

    private bool _prevStickEngaged;
    private bool _stickEverEngagedWhileOpen;
    private int _lastSectorWhileEngaged = -1;

    public string Id => _activeRadial?.Id ?? string.Empty;

    public IReadOnlySet<GamepadButtons> ActiveChord => _activeChord;

    public RadialMenuDefinition? ActiveRadial => _activeRadial;
    public int CurrentSelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (_selectedIndex != value)
            {
                _selectedIndex = value;
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

                _lastSectorWhileEngaged = index;
                CurrentSelectedIndex = index;
            }
        }
        else
        {
            if (confirmMode == RadialMenuConfirmMode.ReturnStickToCenter &&
                _prevStickEngaged &&
                _stickEverEngagedWhileOpen &&
                _lastSectorWhileEngaged >= 0)
            {
                CurrentSelectedIndex = _lastSectorWhileEngaged;
                TryClose(_activeRadial.Id, string.Empty, true);
                _prevStickEngaged = false;
                return;
            }

            if (CurrentSelectedIndex != -1)
            {
                CurrentSelectedIndex = -1;
            }
        }

        _prevStickEngaged = engaged;
    }

    public void HandleButtonReleased(GamepadButtons releasedButton)
    {
        if (_activeRadial is null || _openMapping is null)
            return;

        if (_activeChord.Contains(releasedButton))
        {
            var confirmOnRelease = _getConfirmMode() != RadialMenuConfirmMode.ReturnStickToCenter;
            CloseInternal(confirmOnRelease, confirmOnRelease);
        }
        else if (_activeChord.Count == 0 && _activeRadial != null)
        {
            // FALLBACK: If we have an active radial but no chord (e.g. opened via a non-button trigger),
            // still allow standard release logic if applicable.
        }
    }

    public void ForceCancel()
    {
        ForceReset();
    }

    public void SetDefinitions(List<RadialMenuDefinition>? radialMenus, List<KeyboardActionDefinition>? keyboardActions, IKeyboardActionCatalog? catalog = null)
    {
        _radialMenus = radialMenus;
        _keyboardActions = keyboardActions;
        _catalog = catalog;
    }

    public bool TryOpen(MappingEntry mapping, string sourceToken, out string? errorStatus)
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

    public bool TryClose(string radialMenuId, string sourceToken, bool dispatchSelection, bool suppressChord = false)
    {
        if (_activeRadial is null)
            return false;

        if (!string.Equals(radialMenuId, _activeRadial.Id, StringComparison.Ordinal))
            return false;

        CloseInternal(dispatchSelection, suppressChord);
        return true;
    }

    public void ForceReset()
    {
        _activeChord.Clear();
        if (_activeRadial is null)
            return;

        _runOnUi(() => _radialMenuHud.HideMenu());
        var id = _activeRadial.Id;
        ClearState();
        _unregisterActiveAction(id);
    }

    private void CloseInternal(bool dispatchSelection, bool suppressChord)
    {
        if (_activeRadial is null) return;

        var selectedIndex = _selectedIndex;
        var definition = _activeRadial;
        var sourceToken = string.IsNullOrEmpty(_sourceToken) ? "RadialMenu" : _sourceToken;
        var openMapping = _openMapping;
        var id = definition.Id;

        _runOnUi(() => _radialMenuHud.HideMenu());

        if (dispatchSelection && selectedIndex >= 0 && selectedIndex < definition.Items.Count)
        {
            var item = definition.Items[selectedIndex];
            var action = _catalog?.GetAction(item.ActionId)
                         ?? _keyboardActions?.FirstOrDefault(a =>
                             string.Equals(a.Id, item.ActionId, StringComparison.OrdinalIgnoreCase));

            if (action?.TemplateToggle != null)
            {
                var targetId = action.TemplateToggle.AlternateProfileId;
                _setMappingStatus($"Radial Menu Selection: {item.ActionId} -> Toggle profile {targetId}");
                _setMappedOutput($"Toggle profile → {targetId}");
                _requestTemplateSwitch?.Invoke(targetId);
            }
            else
            {
                var keyToken = action?.KeyboardKey ?? string.Empty;

                if (InputTokenResolver.TryResolveMappedOutput(keyToken, out var output, out var baseLabel))
                {
                    var outputLabel = $"{baseLabel} (Radial)";
                    _setMappedOutput(outputLabel);
                    _setMappingStatus($"Radial Menu Selection: {item.ActionId} -> {outputLabel}");
                    _enqueueOutput(sourceToken, TriggerMoment.Tap, output, outputLabel, keyToken);
                }
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
