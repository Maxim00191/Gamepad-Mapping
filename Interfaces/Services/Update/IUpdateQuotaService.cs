using GamepadMapperGUI.Models.Core;
using System.Threading.Tasks;

namespace GamepadMapperGUI.Interfaces.Services.Update;

public interface IUpdateQuotaService
{
    Task<UpdateQuotaDecision> TryConsumeQuotaAsync(UpdateQuotaAction action);
}

