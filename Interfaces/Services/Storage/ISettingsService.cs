using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Interfaces.Services.Storage;

public interface ISettingsService
{
    AppSettings LoadSettings();
    void SaveSettings(AppSettings settings);
}

