using System.Collections.Generic;
using System.Collections.ObjectModel;
using GamepadMapperGUI.Utils;
using Newtonsoft.Json;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GamepadMapperGUI.Models;

public partial class RadialMenuDefinition : ObservableObject
{
    private string _id = string.Empty;
    private string _displayName = string.Empty;
    private string _joystick = "RightStick";
    private Dictionary<string, string>? _displayNames;

    [JsonProperty("id")]
    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    [JsonProperty("displayName")]
    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    /// <summary>Optional per-culture titles (e.g. <c>zh-CN</c>). Applied on template load; overrides <see cref="DisplayName"/> when the UI culture matches.</summary>
    [JsonProperty("displayNames", NullValueHandling = NullValueHandling.Ignore)]
    public Dictionary<string, string>? DisplayNames
    {
        get => _displayNames;
        set
        {
            if (!SetProperty(ref _displayNames, value))
                return;
            OnPropertyChanged(nameof(DisplayNameZhCn));
            OnPropertyChanged(nameof(DisplayNameEnUs));
        }
    }

    /// <summary>Editor binding for <c>displayNames["zh-CN"]</c>.</summary>
    [JsonIgnore]
    public string DisplayNameZhCn
    {
        get => LocalizedCultureStringMap.Get(DisplayNames, TemplateLocaleKeys.ZhCn);
        set
        {
            var next = LocalizedCultureStringMap.WithCulture(DisplayNames, TemplateLocaleKeys.ZhCn, value);
            if (LocalizedCultureStringMap.ContentEquals(_displayNames, next))
                return;
            _displayNames = next;
            OnPropertyChanged(nameof(DisplayNames));
            OnPropertyChanged(nameof(DisplayNameZhCn));
        }
    }

    /// <summary>Editor binding for <c>displayNames["en-US"]</c>.</summary>
    [JsonIgnore]
    public string DisplayNameEnUs
    {
        get => LocalizedCultureStringMap.Get(DisplayNames, TemplateLocaleKeys.EnUs);
        set
        {
            var next = LocalizedCultureStringMap.WithCulture(DisplayNames, TemplateLocaleKeys.EnUs, value);
            if (LocalizedCultureStringMap.ContentEquals(_displayNames, next))
                return;
            _displayNames = next;
            OnPropertyChanged(nameof(DisplayNames));
            OnPropertyChanged(nameof(DisplayNameEnUs));
        }
    }

    /// <summary>
    /// Which joystick to use for selection: "LeftStick" or "RightStick"
    /// </summary>
    [JsonProperty("joystick")]
    public string Joystick
    {
        get => _joystick;
        set => SetProperty(ref _joystick, value);
    }

    [JsonProperty("items")]
    public ObservableCollection<RadialMenuItem> Items { get; set; } = new();
}
