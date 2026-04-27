using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamepadMapperGUI.Core;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Storage;
using GamepadMapperGUI.Interfaces.Services.Update;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Interfaces.Services.Radial;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Services.Infrastructure;

namespace Gamepad_Mapping.ViewModels.Strategies;

public partial class ItemCycleActionEditorViewModel : ActionEditorViewModelBase
{
    private readonly IKeyboardCaptureService _keyboardCaptureService;

    public ItemCycleActionEditorViewModel(IKeyboardCaptureService keyboardCaptureService)
    {
        _keyboardCaptureService = keyboardCaptureService;
    }

    [ObservableProperty]
    private ItemCycleDirection _direction = ItemCycleDirection.Next;

    [ObservableProperty]
    private string _slotCountText = "9";

    [ObservableProperty]
    private string _withKeys = string.Empty;

    [ObservableProperty]
    private string _forwardKey = string.Empty;

    [ObservableProperty]
    private string _backwardKey = string.Empty;

    [RelayCommand]
    private void RecordForwardKey()
    {
        _keyboardCaptureService.BeginCapture(
            AppUiLocalization.GetString(AppUiLocalization.KeyboardCapturePromptKeys.ItemCycleForward),
            key => ForwardKey = key.ToString());
    }

    [RelayCommand]
    private void RecordBackwardKey()
    {
        _keyboardCaptureService.BeginCapture(
            AppUiLocalization.GetString(AppUiLocalization.KeyboardCapturePromptKeys.ItemCycleBackward),
            key => BackwardKey = key.ToString());
    }

    public override void SyncFrom(MappingEntry mapping)
    {
        if (mapping.ItemCycle is { } ic)
        {
            Direction = ic.Direction;
            SlotCountText = Math.Clamp(ic.SlotCount, 1, 9).ToString(CultureInfo.InvariantCulture);
            WithKeys = ic.WithKeys is { Count: > 0 } ? string.Join('+', ic.WithKeys) : string.Empty;
            ForwardKey = ic.LoopForwardKey ?? string.Empty;
            BackwardKey = ic.LoopBackwardKey ?? string.Empty;
        }
    }

    public override bool ApplyTo(MappingEntry mapping)
    {
        if (!int.TryParse(SlotCountText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) || n < 1 || n > 9)
            return false;

        if (!TryParseWithKeysTokens(WithKeys, out var withKeys))
            return false;

        var fwdRaw = (ForwardKey ?? string.Empty).Trim();
        var backRaw = (BackwardKey ?? string.Empty).Trim();
        var hasFwd = fwdRaw.Length > 0;
        var hasBack = backRaw.Length > 0;
        if (hasFwd != hasBack) return false;

        ResetCommonMappingFields(mapping);

        if (hasFwd)
        {
            if (!MappingEngine.TryNormalizeMappedOutputStorage(fwdRaw, out var fSt) ||
                !MappingEngine.TryNormalizeMappedOutputStorage(backRaw, out var bSt))
                return false;

            mapping.ItemCycle = new ItemCycleBinding
            {
                Direction = Direction,
                SlotCount = n,
                LoopForwardKey = fSt,
                LoopBackwardKey = bSt,
                WithKeys = withKeys
            };
        }
        else
        {
            mapping.ItemCycle = new ItemCycleBinding
            {
                Direction = Direction,
                SlotCount = n,
                WithKeys = withKeys
            };
        }

        return true;
    }

    public override void Clear()
    {
        Direction = ItemCycleDirection.Next;
        SlotCountText = "9";
        WithKeys = string.Empty;
        ForwardKey = string.Empty;
        BackwardKey = string.Empty;
    }

    private static bool TryParseWithKeysTokens(string? line, out List<string>? tokens)
    {
        tokens = null;
        if (string.IsNullOrWhiteSpace(line)) return true;

        var parts = line.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return true;

        tokens = [];
        foreach (var p in parts)
        {
            if (MappingEngine.ParseKey(p) == System.Windows.Input.Key.None) return false;
            tokens.Add(p);
        }
        return true;
    }
}

