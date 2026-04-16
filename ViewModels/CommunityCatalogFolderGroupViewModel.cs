using System.Collections.ObjectModel;
using System.Linq;

namespace Gamepad_Mapping.ViewModels;

public sealed class CommunityCatalogFolderGroupViewModel
{
    public CommunityCatalogFolderGroupViewModel(
        string folderName,
        ObservableCollection<CommunityCatalogAuthorGroupViewModel> authorGroups)
    {
        FolderName = folderName;
        AuthorGroups = authorGroups;
    }

    public string FolderName { get; }

    public ObservableCollection<CommunityCatalogAuthorGroupViewModel> AuthorGroups { get; }

    public int TemplateCount => AuthorGroups.Sum(static group => group.Templates.Count);
}
