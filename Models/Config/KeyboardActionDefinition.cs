using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;

namespace GamepadMapperGUI.Models;

/// <summary>One game action in the profile's keyboard catalog; referenced by <see cref="MappingEntry.ActionId"/>.</summary>
public sealed class KeyboardActionDefinition : ObservableObject
{
    private string _id = string.Empty;
    [JsonProperty("id")]
    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    private string? _keyboardKey;
    [JsonProperty("keyboardKey", NullValueHandling = NullValueHandling.Ignore)]
    public string? KeyboardKey
    {
        get => _keyboardKey;
        set => SetProperty(ref _keyboardKey, value);
    }

    private TemplateToggleBinding? _templateToggle;
    [JsonProperty("templateToggle", NullValueHandling = NullValueHandling.Ignore)]
    public TemplateToggleBinding? TemplateToggle
    {
        get => _templateToggle;
        set => SetProperty(ref _templateToggle, value);
    }

    private RadialMenuBinding? _radialMenu;
    [JsonProperty("radialMenu", NullValueHandling = NullValueHandling.Ignore)]
    public RadialMenuBinding? RadialMenu
    {
        get => _radialMenu;
        set => SetProperty(ref _radialMenu, value);
    }

    private string _description = string.Empty;
    [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
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

    private Dictionary<string, string>? _descriptions;
    [JsonProperty("descriptions", NullValueHandling = NullValueHandling.Ignore)]
    public Dictionary<string, string>? Descriptions
    {
        get => _descriptions;
        set => SetProperty(ref _descriptions, value);
    }

    /// <summary>
    /// Logical abstraction: returns true if the action defines any actual output.
    /// </summary>
    [JsonIgnore]
    public bool HasOutput =>
        !string.IsNullOrWhiteSpace(KeyboardKey) ||
        TemplateToggle != null ||
        RadialMenu != null;
}
