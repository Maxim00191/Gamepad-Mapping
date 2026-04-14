using GamepadMapperGUI.Models.Core;
using GamepadMapperGUI.Models.Core.Community;

namespace GamepadMapperGUI.Interfaces.Services.Infrastructure;

public interface ICommunityTemplateUploadComplianceService
{
    CommunityTemplateUploadComplianceResult EvaluateSubmission(
        IReadOnlyList<CommunityTemplateBundleEntry> selectedEntries,
        string gameFolderDisplayName,
        string authorDisplayName,
        string listingDescription);
}
