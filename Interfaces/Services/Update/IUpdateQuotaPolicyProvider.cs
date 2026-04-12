using GamepadMapperGUI.Models.Core;

namespace GamepadMapperGUI.Interfaces.Services.Update;

public interface IUpdateQuotaPolicyProvider
{
    UpdateQuotaPolicy GetCurrentPolicy();
}

