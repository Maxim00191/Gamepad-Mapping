using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GamepadMapperGUI.Models;

[JsonConverter(typeof(StringEnumConverter))]
public enum TriggerMoment
{
    Pressed,
    Released,
    Tap
}

public class MappingEntry : ObservableObject
{
    private GamepadBinding _from = new();

    [JsonProperty("from")]
    public GamepadBinding From
    {
        get => _from;
        set => SetProperty(ref _from, value);
    }

    private string _keyboardKey = string.Empty;

    [JsonProperty("keyboardKey")]
    public string KeyboardKey
    {
        get => _keyboardKey;
        set
        {
            if (SetProperty(ref _keyboardKey, value))
                OnPropertyChanged(nameof(OutputSummaryForGrid));
        }
    }

    private string _description = string.Empty;

    [JsonProperty("description")]
    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    private string? _descriptionKey;

    [JsonProperty("descriptionKey", NullValueHandling = NullValueHandling.Ignore)]
    public string? DescriptionKey
    {
        get => _descriptionKey;
        set => SetProperty(ref _descriptionKey, value);
    }

    /// <summary>Optional per-culture descriptions (e.g. <c>"zh-CN"</c>). Overrides <see cref="Description"/> and resource <see cref="DescriptionKey"/> when the current UI culture matches.</summary>
    [JsonProperty("descriptions", NullValueHandling = NullValueHandling.Ignore)]
    public Dictionary<string, string>? Descriptions { get; set; }

    private TriggerMoment _trigger = TriggerMoment.Pressed;

    [JsonProperty("trigger")]
    public TriggerMoment Trigger
    {
        get => _trigger;
        set => SetProperty(ref _trigger, value);
    }

    // Optional: used when From.Type is LeftTrigger/RightTrigger etc.
    // When omitted, consumers can fall back to a sensible default.
    private float? _analogThreshold;

    [JsonProperty("analogThreshold")]
    public float? AnalogThreshold
    {
        get => _analogThreshold;
        set => SetProperty(ref _analogThreshold, value);
    }

    /// <summary>
    /// When set with <see cref="HoldThresholdMs"/> and <see cref="Trigger"/> is <see cref="TriggerMoment.Tap"/>,
    /// a short press/release before the threshold sends <see cref="KeyboardKey"/> once; holding at least that long
    /// sends <see cref="HoldKeyboardKey"/> once (button-only chords; no LT/RT modifiers in the chord).
    /// </summary>
    private string _holdKeyboardKey = string.Empty;

    [JsonProperty("holdKeyboardKey")]
    public string HoldKeyboardKey
    {
        get => _holdKeyboardKey;
        set => SetProperty(ref _holdKeyboardKey, value);
    }

    /// <summary>Minimum hold duration in milliseconds for the hold output (typical range 250–800).</summary>
    private int? _holdThresholdMs;

    [JsonProperty("holdThresholdMs")]
    public int? HoldThresholdMs
    {
        get => _holdThresholdMs;
        set => SetProperty(ref _holdThresholdMs, value);
    }

    private ItemCycleBinding? _itemCycle;

    /// <summary>When set, this mapping advances a shared 1..n slot and taps digit keys or custom loop keys instead of <see cref="KeyboardKey"/>.</summary>
    [JsonProperty("itemCycle", NullValueHandling = NullValueHandling.Ignore)]
    public ItemCycleBinding? ItemCycle
    {
        get => _itemCycle;
        set
        {
            if (SetProperty(ref _itemCycle, value))
                OnPropertyChanged(nameof(OutputSummaryForGrid));
        }
    }

    private TemplateToggleBinding? _templateToggle;

    /// <summary>When set, activates another profile instead of <see cref="KeyboardKey"/> (mutually exclusive with <see cref="ItemCycle"/>).</summary>
    [JsonProperty("templateToggle", NullValueHandling = NullValueHandling.Ignore)]
    public TemplateToggleBinding? TemplateToggle
    {
        get => _templateToggle;
        set
        {
            if (SetProperty(ref _templateToggle, value))
                OnPropertyChanged(nameof(OutputSummaryForGrid));
        }
    }

    /// <summary>Compact label for the mapping grid (item cycle summary or <see cref="KeyboardKey"/>).</summary>
    [JsonIgnore]
    public string OutputSummaryForGrid
    {
        get
        {
            if (ItemCycle is { } ic)
            {
                var n = Math.Clamp(ic.SlotCount, 1, 9);
                var mods = ic.WithKeys is { Count: > 0 }
                    ? string.Join("+", ic.WithKeys) + "+"
                    : string.Empty;
                var dir = ic.Direction == ItemCycleDirection.Previous ? "prev" : "next";
                var fwd = ic.LoopForwardKey?.Trim() ?? string.Empty;
                var back = ic.LoopBackwardKey?.Trim() ?? string.Empty;
                if (fwd.Length > 0 && back.Length > 0)
                    return $"{mods}{fwd} / {back} ({dir}, 1–{n})";
                return $"{mods}Items 1–{n} ({dir})";
            }

            if (TemplateToggle is { } tt)
            {
                var id = tt.AlternateProfileId?.Trim() ?? string.Empty;
                return id.Length > 0 ? $"Toggle profile → {id}" : "Toggle profile";
            }

            return _keyboardKey ?? string.Empty;
        }
    }
}
