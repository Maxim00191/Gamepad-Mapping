using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GamepadMapperGUI.Models;

public sealed partial class RadialMenuItem : ObservableObject
{
    private string _actionId = string.Empty;
    private string? _icon;
    private string _label = string.Empty;

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
}
