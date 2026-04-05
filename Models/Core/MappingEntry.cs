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

    private string? _actionId;

    /// <summary>When set, <see cref="KeyboardKey"/> is taken from <see cref="GameProfileTemplate.KeyboardActions"/> on load (see ProfileService).</summary>
    [JsonProperty("actionId", NullValueHandling = NullValueHandling.Ignore)]
    public string? ActionId
    {
        get => _actionId;
        set => SetProperty(ref _actionId, value);
    }

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

    /// <summary>Omit redundant <c>keyboardKey</c> when the mapping uses the <c>keyboardActions</c> catalog.</summary>
    public bool ShouldSerializeKeyboardKey() => string.IsNullOrWhiteSpace(_actionId);

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

    private string _templateToggleDisplayName = string.Empty;

    /// <summary>Resolved display name for <see cref="TemplateToggle"/> target (set by the view-model).</summary>
    [JsonIgnore]
    public string TemplateToggleDisplayName
    {
        get => _templateToggleDisplayName;
        set
        {
            if (SetProperty(ref _templateToggleDisplayName, value))
                OnPropertyChanged(nameof(OutputSummaryForGrid));
        }
    }

    private RadialMenuBinding? _radialMenu;

    /// <summary>When set, pressing the trigger button opens a radial menu for selection via joystick.</summary>
    [JsonProperty("radialMenu", NullValueHandling = NullValueHandling.Ignore)]
    public RadialMenuBinding? RadialMenu
    {
        get => _radialMenu;
        set
        {
            if (SetProperty(ref _radialMenu, value))
                OnPropertyChanged(nameof(OutputSummaryForGrid));
        }
    }

    /// <summary>Compact label for the mapping grid (item cycle summary or <see cref="KeyboardKey"/>).</summary>
    [JsonIgnore]
    public string OutputSummaryForGrid
    {
        get
        {
            if (RadialMenu is { } rm)
            {
                return $"Radial Menu: {rm.RadialMenuId}";
            }

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
                var display = string.IsNullOrWhiteSpace(_templateToggleDisplayName) ? id : _templateToggleDisplayName;
                return id.Length > 0 ? $"Toggle profile → {display}" : "Toggle profile";
            }

            return _keyboardKey ?? string.Empty;
        }
    }

    internal void ApplyKeyboardActionResolution(string keyboardKey, string? defaultDescription)
    {
        if (!string.Equals(_keyboardKey, keyboardKey, StringComparison.Ordinal))
        {
            _keyboardKey = keyboardKey ?? string.Empty;
            OnPropertyChanged(nameof(KeyboardKey));
            OnPropertyChanged(nameof(OutputSummaryForGrid));
        }

        if (!string.IsNullOrWhiteSpace(defaultDescription) && string.IsNullOrWhiteSpace(_description))
        {
            _description = defaultDescription.Trim();
            OnPropertyChanged(nameof(Description));
        }
    }

    /// <summary>Applies <paramref name="def"/> to <see cref="KeyboardKey"/> and <see cref="Description"/> when the mapping is bound via <see cref="ActionId"/> in the editor.</summary>
    internal void ApplyKeyboardCatalogDefinition(KeyboardActionDefinition def)
    {
        ArgumentNullException.ThrowIfNull(def);

        var key = (def.KeyboardKey ?? string.Empty).Trim();
        if (!string.Equals(_keyboardKey, key, StringComparison.Ordinal))
        {
            _keyboardKey = key;
            OnPropertyChanged(nameof(KeyboardKey));
            OnPropertyChanged(nameof(OutputSummaryForGrid));
        }

        var desc = (def.Description ?? string.Empty).Trim();
        if (!string.Equals(_description, desc, StringComparison.Ordinal))
        {
            _description = desc;
            OnPropertyChanged(nameof(Description));
        }
    }
}
