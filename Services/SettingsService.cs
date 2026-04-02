using System;
using System.IO;
using System.Text;
using GamepadMapperGUI.Interfaces.Services;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Utils;
using Newtonsoft.Json;

namespace GamepadMapperGUI.Services;

public class SettingsService : ISettingsService
{
    private static readonly string DefaultSettingsPath = Path.Combine("Assets", "Config", "default_settings.json");
    private static readonly string LocalSettingsPath = Path.Combine("Assets", "Config", "local_settings.json");

    private readonly IFileSystem _fileSystem;
    private readonly IPathProvider _pathProvider;

    public SettingsService(IFileSystem? fileSystem = null, IPathProvider? pathProvider = null)
    {
        _fileSystem = fileSystem ?? new PhysicalFileSystem();
        _pathProvider = pathProvider ?? new AppPathProvider();
    }

    // Instance method for interface implementation
    AppSettings ISettingsService.LoadSettings() => LoadSettingsInternal();
    void ISettingsService.SaveSettings(AppSettings settings) => SaveSettingsInternal(settings);

    // Internal implementation
    public AppSettings LoadSettingsInternal()
    {
        var root = _pathProvider.GetContentRoot();

        var defaultPath = Path.Combine(root, DefaultSettingsPath);
        var localPath = Path.Combine(root, LocalSettingsPath);

        if (!_fileSystem.FileExists(localPath) && _fileSystem.FileExists(defaultPath))
        {
            _fileSystem.CreateDirectory(_fileSystem.GetDirectoryName(localPath)!);
            _fileSystem.CopyFile(defaultPath, localPath, overwrite: false);
        }

        var pathToLoad = _fileSystem.FileExists(localPath) ? localPath : defaultPath;
        if (!_fileSystem.FileExists(pathToLoad))
        {
            return new AppSettings();
        }

        try
        {
            var json = _fileSystem.ReadAllText(pathToLoad, Encoding.UTF8);
            var settings = JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
            NormalizeTriggerDeadzones(settings);
            return settings;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load settings from {pathToLoad}: {ex.Message}");
            return new AppSettings();
        }
    }

    public void SaveSettingsInternal(AppSettings settings)
    {
        if (settings is null) throw new ArgumentNullException(nameof(settings));

        var root = _pathProvider.GetContentRoot();
        var localPath = Path.Combine(root, LocalSettingsPath);
        _fileSystem.CreateDirectory(_fileSystem.GetDirectoryName(localPath)!);
        var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
        _fileSystem.WriteAllText(localPath, json, Encoding.UTF8);
    }

    // Static version for backward compatibility
    public static AppSettings LoadSettings() => new SettingsService().LoadSettingsInternal();
    public static void SaveSettings(AppSettings settings) => new SettingsService().SaveSettingsInternal(settings);

    private const float TriggerDeadzoneMinSpan = 0.02f;

    public static void NormalizeTriggerDeadzones(AppSettings s)
    {
        static void FixPair(ref float inner, ref float outer)
        {
            inner = Math.Clamp(inner, 0f, 0.98f);
            if (outer < inner + TriggerDeadzoneMinSpan)
                outer = 1f;
            outer = Math.Clamp(outer, inner + TriggerDeadzoneMinSpan, 1f);
        }

        var li = s.LeftTriggerInnerDeadzone;
        var lo = s.LeftTriggerOuterDeadzone;
        var ri = s.RightTriggerInnerDeadzone;
        var ro = s.RightTriggerOuterDeadzone;
        FixPair(ref li, ref lo);
        FixPair(ref ri, ref ro);
        s.LeftTriggerInnerDeadzone = li;
        s.LeftTriggerOuterDeadzone = lo;
        s.RightTriggerInnerDeadzone = ri;
        s.RightTriggerOuterDeadzone = ro;
    }
}
