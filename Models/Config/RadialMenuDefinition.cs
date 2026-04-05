using System.Collections.Generic;
using System.Collections.ObjectModel;
using Newtonsoft.Json;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GamepadMapperGUI.Models;

public partial class RadialMenuDefinition : ObservableObject
{
    private string _id = string.Empty;
    private string _displayName = string.Empty;
    private string _joystick = "RightStick";

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
