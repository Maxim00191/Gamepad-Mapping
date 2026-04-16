using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Models.Core;

namespace GamepadMapperGUI.Interfaces.Services.Infrastructure;

public interface ICommunityTemplateUploadService
{
    Task<CommunityTemplateUploadResult> SubmitBundleAsync(
        IReadOnlyList<GameProfileTemplate> templates,
        string gameFolderDisplayName,
        string authorDisplayName,
        string communityListingDescription,
        CancellationToken cancellationToken = default);
}
