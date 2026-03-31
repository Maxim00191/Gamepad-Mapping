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
        var settings = JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
        NormalizeTriggerDeadzones(settings);
        return settings;
    }

    private const float TriggerDeadzoneMinSpan = 0.02f;

    /// <summary>
    /// Ensures trigger outer &gt; inner and outer defaults to 1 when missing from older JSON (stored as 0).
    /// </summary>
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

    AppSettings ISettingsService.LoadSettings() => LoadSettings();

    public static void SaveSettings(AppSettings settings)
    {
        if (settings is null) throw new ArgumentNullException(nameof(settings));

        var root = AppPaths.ResolveContentRoot();
        var localPath = Path.Combine(root, LocalSettingsPath);
        Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
        var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
        File.WriteAllText(localPath, json, Encoding.UTF8);
    }

    void ISettingsService.SaveSettings(AppSettings settings) => SaveSettings(settings);
}

