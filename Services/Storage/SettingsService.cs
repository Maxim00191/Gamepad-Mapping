using System;
using System.IO;
using System.Linq;
using System.Text;
using GamepadMapperGUI.Services.Infrastructure;
using GamepadMapperGUI.Services.Storage;
using GamepadMapperGUI.Services.Update;
using GamepadMapperGUI.Services.Input;
using GamepadMapperGUI.Services.Radial;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Storage;
using GamepadMapperGUI.Interfaces.Services.Update;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Interfaces.Services.Radial;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GamepadMapperGUI.Services.Storage;

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
            try
            {
                _fileSystem.CopyFile(defaultPath, localPath, overwrite: false);
            }
            catch (IOException) when (_fileSystem.FileExists(localPath))
            {
                // Another concurrent initialization already created local settings.
                // Treat as success and continue loading from localPath.
            }
        }

        if (!_fileSystem.FileExists(defaultPath) && !_fileSystem.FileExists(localPath))
        {
            return new AppSettings();
        }

        string? defaultJson = null;
        string? localJson = null;
        try
        {
            if (_fileSystem.FileExists(defaultPath))
                defaultJson = _fileSystem.ReadAllText(defaultPath, Encoding.UTF8);
            if (_fileSystem.FileExists(localPath))
                localJson = _fileSystem.ReadAllText(localPath, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to read settings files: {ex.Message}");
            return new AppSettings();
        }

        // Preserve legacy behavior: a present but invalid local_settings.json is not silently replaced by defaults.
        if (localJson is not null && AppSettingsJsonMerger.TryParseObject(localJson) is null)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to parse local settings JSON at {localPath}.");
            return new AppSettings();
        }

        try
        {
            var json = AppSettingsJsonMerger.MergeToJsonString(defaultJson, localJson);
            var settings = JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
            NormalizeTriggerDeadzones(settings);
            NormalizeUpdateInstallPolicy(settings);
            settings.UiTheme = UiThemeMode.Normalize(settings.UiTheme);

            // Self-update replaces default_settings.json but preserves local_settings.json. When defaults gain new keys,
            // merge them into the in-memory model and persist so local_settings.json matches (user overrides kept).
            if (ShouldPersistMergedLocalSnapshot(defaultJson, localJson))
            {
                try
                {
                    SaveSettingsInternal(settings);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"Could not sync local_settings.json from default_settings.json: {ex.Message}");
                }
            }

            return settings;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load merged settings: {ex.Message}");
            return new AppSettings();
        }
    }

    /// <summary>
    /// True when merged(default, local) differs from the current local JSON so we should write the merged snapshot to disk.
    /// </summary>
    private static bool ShouldPersistMergedLocalSnapshot(string? defaultJson, string? localJson)
    {
        if (AppSettingsJsonMerger.TryParseObject(defaultJson) is null)
            return false;

        var merged = AppSettingsJsonMerger.TryParseObject(
            AppSettingsJsonMerger.MergeToJsonString(defaultJson, localJson));
        if (merged is null)
            return false;

        if (string.IsNullOrWhiteSpace(localJson))
            return true;

        var local = AppSettingsJsonMerger.TryParseObject(localJson);
        if (local is null)
            return false;

        return !JToken.DeepEquals(merged, local);
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
    private static readonly string[] DefaultPreservePaths =
    [
        "Assets\\Profiles\\templates",
        "Assets\\Config\\local_settings.json"
    ];

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

    private static void NormalizeUpdateInstallPolicy(AppSettings s)
    {
        s.UpdateInstallPolicy ??= new UpdateInstallPolicySettings();
        s.UpdateInstallPolicy.PreservePaths ??= [];

        var normalized = s.UpdateInstallPolicy.PreservePaths
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().Replace('/', '\\').Trim('\\'))
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalized.Count == 0)
            normalized.AddRange(DefaultPreservePaths);

        s.UpdateInstallPolicy.PreservePaths = normalized;
    }
}


