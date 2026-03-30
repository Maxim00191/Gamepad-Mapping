using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Interfaces.Services;

public interface ISettingsService
{
    AppSettings LoadSettings();
}
