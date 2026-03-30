using System.Text;
using System.IO;
using GamepadMapperGUI.Interfaces.Services;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Utils;
using Newtonsoft.Json;

namespace GamepadMapperGUI.Services;

public class SettingsService : ISettingsService
{
    private static readonly string DefaultSettingsPath = Path.Combine("Assets", "Config", "default_settings.json");
    private static readonly string LocalSettingsPath = Path.Combine("Assets", "Config", "local_settings.json");

    public static AppSettings LoadSettings()
    {
        var root = AppPaths.ResolveContentRoot();

        var defaultPath = Path.Combine(root, DefaultSettingsPath);
        var localPath = Path.Combine(root, LocalSettingsPath);

        if (!File.Exists(localPath) && File.Exists(defaultPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
            File.Copy(defaultPath, localPath, overwrite: false);
        }

        var pathToLoad = File.Exists(localPath) ? localPath : defaultPath;
        if (!File.Exists(pathToLoad))
        {
            return new AppSettings();
        }

        var json = File.ReadAllText(pathToLoad, Encoding.UTF8);
        var settings = JsonConvert.DeserializeObject<AppSettings>(json);
        return settings ?? new AppSettings();
    }

    AppSettings ISettingsService.LoadSettings() => LoadSettings();
}

