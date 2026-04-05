using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace GamepadMapperGUI.Models;

public sealed class RadialMenuItem
{
    /// <summary>
    /// Reference to an Id in keyboardActions
    /// </summary>
    [JsonProperty("actionId")]
    public string ActionId { get; set; } = string.Empty;

    /// <summary>
    /// Optional icon path or key
    /// </summary>
    [JsonProperty("icon", NullValueHandling = NullValueHandling.Ignore)]
    public string? Icon { get; set; }

    /// <summary>Optional HUD line for this slot; falls back to the keyboard action description when empty after localization.</summary>
    [JsonProperty("label", NullValueHandling = NullValueHandling.Ignore)]
    public string Label { get; set; } = string.Empty;

    /// <summary>Optional resource key resolved via <see cref="GamepadMapperGUI.Services.TranslationService"/>.</summary>
    [JsonProperty("labelKey", NullValueHandling = NullValueHandling.Ignore)]
    public string? LabelKey { get; set; }

    /// <summary>Optional per-culture HUD labels (e.g. <c>zh-CN</c>), same pattern as <see cref="KeyboardActionDefinition.Descriptions"/>.</summary>
    [JsonProperty("labels", NullValueHandling = NullValueHandling.Ignore)]
    public Dictionary<string, string>? Labels { get; set; }

    [JsonIgnore]
    public string LabelZhCn
    {
        get => Labels != null && Labels.TryGetValue("zh-CN", out var v) ? v : string.Empty;
        set
        {
            Labels ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(value))
                Labels.Remove("zh-CN");
            else
                Labels["zh-CN"] = value.Trim();
            if (Labels.Count == 0)
                Labels = null;
        }
    }

    [JsonIgnore]
    public string LabelEnUs
    {
        get => Labels != null && Labels.TryGetValue("en-US", out var v) ? v : string.Empty;
        set
        {
            Labels ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(value))
                Labels.Remove("en-US");
            else
                Labels["en-US"] = value.Trim();
            if (Labels.Count == 0)
                Labels = null;
        }
    }
}
