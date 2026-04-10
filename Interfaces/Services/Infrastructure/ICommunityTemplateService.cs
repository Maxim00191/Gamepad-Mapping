using System.Collections.Generic;
using System.Threading.Tasks;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Models.Core;

namespace GamepadMapperGUI.Interfaces.Services.Infrastructure;

public interface ICommunityTemplateService
{
    Task<List<CommunityTemplateInfo>> GetTemplatesAsync();
    Task<CommunityTemplateDownloadPrecheckResult> CheckLocalTemplateConflictAsync(CommunityTemplateInfo template);
    Task<bool> DownloadTemplateAsync(CommunityTemplateInfo template, bool allowOverwrite = true);
}

