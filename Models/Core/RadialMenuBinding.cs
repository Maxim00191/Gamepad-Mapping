using Newtonsoft.Json;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GamepadMapperGUI.Models;

public partial class RadialMenuBinding : ObservableObject
{
    private string _radialMenuId = string.Empty;

    [JsonProperty("radialMenuId")]
    public string RadialMenuId
    {
        get => _radialMenuId;
        set => SetProperty(ref _radialMenuId, value);
    }
}
