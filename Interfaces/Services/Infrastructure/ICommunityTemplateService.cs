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
    /// Fetches index.json. Returns null when the index could not be loaded.
    /// <see cref="CommunityIndexFetchBehavior.PreferFreshIndex"/> favors GitHub raw and adds a one-time cache-busting query for edge caches.
    /// </summary>
    Task<List<CommunityTemplateInfo>?> GetCommunityIndexSnapshotAsync(
        CancellationToken cancellationToken = default,
        CommunityIndexFetchBehavior fetchBehavior = CommunityIndexFetchBehavior.Default);

    Task<bool> IsTemplateDownloadedAsync(CommunityTemplateInfo template);
    Task<CommunityTemplateDownloadPrecheckResult> CheckLocalTemplateConflictAsync(CommunityTemplateInfo template);
    Task<bool> DeleteLocalTemplateAsync(CommunityTemplateInfo template);
    Task<CommunityTemplateDownloadResult> DownloadTemplateAsync(CommunityTemplateInfo template, bool allowOverwrite = true);
}

