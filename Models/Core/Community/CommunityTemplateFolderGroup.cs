using System.Collections.ObjectModel;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Models.Core;

public sealed class CommunityTemplateFolderGroup
{
    public CommunityTemplateFolderGroup(string folderName, ObservableCollection<CommunityTemplateInfo> templates)
    {
        FolderName = folderName;
        Templates = templates;
    }

    public string FolderName { get; }

    public ObservableCollection<CommunityTemplateInfo> Templates { get; }
}
