using System.Collections.ObjectModel;

namespace Gamepad_Mapping.ViewModels;

public sealed class CommunityCatalogFolderGroupViewModel
{
    public CommunityCatalogFolderGroupViewModel(
        string folderName,
        ObservableCollection<CommunityCatalogTemplateItemViewModel> templates)
    {
        FolderName = folderName;
        Templates = templates;
    }

    public string FolderName { get; }

    public ObservableCollection<CommunityCatalogTemplateItemViewModel> Templates { get; }
}
