using System.Collections.Generic;
using GamepadMapperGUI.Utils;
using Newtonsoft.Json;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GamepadMapperGUI.Models;

public sealed partial class RadialMenuItem : ObservableObject
{
    private string _actionId = string.Empty;
    private string? _icon;
    private string _label = string.Empty;
    private Dictionary<string, string>? _labels;

    /// <summary>
    /// Reference to an Id in keyboardActions
    /// </summary>
    [JsonProperty("actionId")]
    public string ActionId
    {
        get => _actionId;
        set => SetProperty(ref _actionId, value);
    }

    /// <summary>
    /// Optional icon path or key
    /// </summary>
    [JsonProperty("icon", NullValueHandling = NullValueHandling.Ignore)]
    public string? Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }

    /// <summary>Optional HUD line for this slot; falls back to the keyboard action description when empty after localization.</summary>
    [JsonProperty("label", NullValueHandling = NullValueHandling.Ignore)]
    public string Label
    {
        get => _label;
        set => SetProperty(ref _label, value);
    }

    /// <summary>Optional per-culture HUD lines for this slot. Applied on template load; overrides <see cref="Label"/> when the UI culture matches.</summary>
    [JsonProperty("labels", NullValueHandling = NullValueHandling.Ignore)]
    public Dictionary<string, string>? Labels
    {
        get => _labels;
        set
        {
            if (!SetProperty(ref _labels, value))
                return;
            OnPropertyChanged(nameof(LabelZhCn));
            OnPropertyChanged(nameof(LabelEnUs));
        }
    }

    /// <summary>Editor binding for <c>labels["zh-CN"]</c>.</summary>
    [JsonIgnore]
    public string LabelZhCn
    {
        get => LocalizedCultureStringMap.Get(Labels, TemplateLocaleKeys.ZhCn);
        set
        {
            var next = LocalizedCultureStringMap.WithCulture(Labels, TemplateLocaleKeys.ZhCn, value);
            if (LocalizedCultureStringMap.ContentEquals(_labels, next))
                return;
            _labels = next;
            OnPropertyChanged(nameof(Labels));
            OnPropertyChanged(nameof(LabelZhCn));
        }
    }

    /// <summary>Editor binding for <c>labels["en-US"]</c>.</summary>
    [JsonIgnore]
    public string LabelEnUs
    {
        get => LocalizedCultureStringMap.Get(Labels, TemplateLocaleKeys.EnUs);
        set
        {
            var next = LocalizedCultureStringMap.WithCulture(Labels, TemplateLocaleKeys.EnUs, value);
            if (LocalizedCultureStringMap.ContentEquals(_labels, next))
                return;
            _labels = next;
            OnPropertyChanged(nameof(Labels));
            OnPropertyChanged(nameof(LabelEnUs));
        }
    }
}
