using GamepadMapperGUI.Models.Core;

namespace GamepadMapperGUI.Interfaces.Services;

public interface IUpdateQuotaPolicyProvider
{
    UpdateQuotaPolicy GetCurrentPolicy();
}
