using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Models.Core;

namespace GamepadMapperGUI.Interfaces.Services.Infrastructure;

public interface ICommunityTemplateService
{
    Task<List<CommunityTemplateInfo>> GetTemplatesAsync();

    /// <summary>
    /// Fetches index.json without the UI refresh throttle. Returns null when the index could not be loaded.
    /// </summary>
    Task<List<CommunityTemplateInfo>?> GetCommunityIndexSnapshotAsync(CancellationToken cancellationToken = default);

    Task<CommunityTemplateDownloadPrecheckResult> CheckLocalTemplateConflictAsync(CommunityTemplateInfo template);
    Task<bool> DownloadTemplateAsync(CommunityTemplateInfo template, bool allowOverwrite = true);
}

