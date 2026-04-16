using System.Collections.ObjectModel;

namespace Gamepad_Mapping.ViewModels;

public sealed class CommunityCatalogAuthorGroupViewModel
{
    public CommunityCatalogAuthorGroupViewModel(
        string authorName,
        ObservableCollection<CommunityCatalogTemplateItemViewModel> templates)
    {
        AuthorName = authorName;
        Templates = templates;
    }

    public string AuthorName { get; }

    public ObservableCollection<CommunityCatalogTemplateItemViewModel> Templates { get; }
}
