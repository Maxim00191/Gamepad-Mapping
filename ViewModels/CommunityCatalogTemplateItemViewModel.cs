using CommunityToolkit.Mvvm.ComponentModel;
using GamepadMapperGUI.Models;

namespace Gamepad_Mapping.ViewModels;

public partial class CommunityCatalogTemplateItemViewModel : ObservableObject
{
    public CommunityCatalogTemplateItemViewModel(CommunityTemplateInfo template)
    {
        Template = template;
    }

    public CommunityTemplateInfo Template { get; }

    public string Id => Template.Id;
    public string DisplayName => Template.DisplayName;
    public string Author => Template.Author;
    public string Description => Template.Description;

    [ObservableProperty]
    private bool _isInstalledLocally;
}
