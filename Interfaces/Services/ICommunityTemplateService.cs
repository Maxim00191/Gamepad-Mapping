using System.Collections.Generic;
using System.Threading.Tasks;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Interfaces.Services;

public interface ICommunityTemplateService
{
    Task<List<CommunityTemplateInfo>> GetTemplatesAsync();
    Task<bool> DownloadTemplateAsync(CommunityTemplateInfo template);
}
