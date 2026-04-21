using CommunityToolkit.Mvvm.ComponentModel;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Services.Infrastructure;

namespace Gamepad_Mapping.ViewModels;

public partial class CommunityCatalogTemplateItemViewModel : ObservableObject
{
    public CommunityCatalogTemplateItemViewModel(CommunityTemplateInfo template)
    {
        Template = template;
    }

    public CommunityTemplateInfo Template { get; }

    public string Id => Template.Id;

    /// <summary>Canonical default title from <c>index.json</c> (English baseline).</summary>
    public string BaselineDisplayName => Template.DisplayName;

    /// <summary>Title for the current UI language (<c>displayNames</c> / <c>displayNameKey</c> / baseline).</summary>
    public string ResolvedDisplayName =>
        CommunityTemplateDisplayLabels.ResolveDisplayName(Template, AppUiLocalization.TryTranslationService());

    public string Author => Template.Author;

    /// <summary>Localized “By author” line for cards and tooltips.</summary>
    public string AuthorCreditLine =>
        string.Format(AppUiLocalization.GetString("CommunityCatalog_AuthorByLine"), Author);

    public string Description => Template.Description;

    [ObservableProperty]
    private bool _isInstalledLocally;
}
