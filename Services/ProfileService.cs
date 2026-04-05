using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Interfaces.Services;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Utils;
using Newtonsoft.Json;

namespace GamepadMapperGUI.Services;

public partial class ProfileService : IProfileService
{
    private static readonly Regex ValidIdPattern = new("^[a-zA-Z0-9][a-zA-Z0-9._-]*$", RegexOptions.Compiled);
    private readonly ISettingsService _settingsService;
    private readonly TranslationService _translationService;
    private readonly IFileSystem _fileSystem;
    private readonly IPathProvider _pathProvider;
    private readonly AppSettings _settings;

    public ProfileService(ISettingsService? settingsService = null, TranslationService? translationService = null, IFileSystem? fileSystem = null, IPathProvider? pathProvider = null)
    {
        _fileSystem = fileSystem ?? new PhysicalFileSystem();
        _pathProvider = pathProvider ?? new AppPathProvider();
        _settingsService = settingsService ?? new SettingsService(_fileSystem, _pathProvider);
        _translationService = translationService
            ?? Application.Current?.Resources["Loc"] as TranslationService
            ?? new TranslationService();
        _settings = _settingsService.LoadSettings();
    }

    public string DefaultProfileId => _settings.DefaultProfileId;

    public string? LastSelectedTemplateProfileId => _settings.LastSelectedTemplateProfileId;

    public void PersistLastSelectedTemplateProfileId(string? profileId)
    {
        _settings.LastSelectedTemplateProfileId = string.IsNullOrWhiteSpace(profileId) ? null : profileId.Trim();
        _settingsService.SaveSettings(_settings);
    }

    public int ModifierGraceMs => Math.Clamp(_settings.ModifierGraceMs, 50, 10_000);

    public int LeadKeyReleaseSuppressMs => Math.Clamp(_settings.LeadKeyReleaseSuppressMs, 50, 10_000);
    public ObservableCollection<TemplateOption> AvailableTemplates { get; } = [];
    public event EventHandler? ProfilesLoaded;

    public string LoadTemplateDirectory()
    {
        var root = _pathProvider.GetContentRoot();
        var templatesDir = Path.Combine(root, _settings.TemplatesDirectory);
        _fileSystem.CreateDirectory(templatesDir);
        return templatesDir;
    }

    public GameProfileTemplate LoadTemplate(string profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
            throw new ArgumentException("Profile id is required.", nameof(profileId));

        var templatePath = Path.Combine(LoadTemplateDirectory(), $"{profileId.Trim()}.json");
        if (!_fileSystem.FileExists(templatePath))
            throw new FileNotFoundException($"Template not found: {templatePath}", templatePath);

        var json = _fileSystem.ReadAllText(templatePath, Encoding.UTF8);
        var template = JsonConvert.DeserializeObject<GameProfileTemplate>(json);
        if (template is null)
            throw new InvalidOperationException($"Failed to parse template JSON: {templatePath}");

        if (string.IsNullOrWhiteSpace(template.ProfileId))
            template.ProfileId = profileId.Trim();

        var culture = _translationService.Culture;

        if (!string.IsNullOrWhiteSpace(template.DisplayNameKey))
        {
            var localized = _translationService[template.DisplayNameKey];
            if (!IsMissingLocalization(localized))
                template.DisplayName = localized;
        }

        if (TryPickCultureString(template.DisplayNames, culture, out var displayForCulture))
            template.DisplayName = displayForCulture;

        if (template.KeyboardActions is { Count: > 0 } keyboardActions)
        {
            foreach (var action in keyboardActions)
            {
                if (!string.IsNullOrWhiteSpace(action.DescriptionKey))
                {
                    var localized = _translationService[action.DescriptionKey];
                    if (!IsMissingLocalization(localized))
                        action.Description = localized;
                }

                if (TryPickCultureString(action.Descriptions, culture, out var actionDesc))
                    action.Description = actionDesc;
            }
        }

        foreach (var mapping in template.Mappings)
        {
            if (!string.IsNullOrWhiteSpace(mapping.DescriptionKey))
            {
                var localized = _translationService[mapping.DescriptionKey];
                if (!IsMissingLocalization(localized))
                    mapping.Description = localized;
            }

            if (TryPickCultureString(mapping.Descriptions, culture, out var descForCulture))
                mapping.Description = descForCulture;
        }

        if (template.RadialMenus is { Count: > 0 } radialMenus)
        {
            foreach (var rm in radialMenus)
            {
                if (!string.IsNullOrWhiteSpace(rm.DisplayNameKey))
                {
                    var localized = _translationService[rm.DisplayNameKey];
                    if (!IsMissingLocalization(localized))
                        rm.DisplayName = localized;
                }

                if (TryPickCultureString(rm.DisplayNames, culture, out var rmTitle))
                    rm.DisplayName = rmTitle;

                foreach (var slot in rm.Items)
                {
                    if (!string.IsNullOrWhiteSpace(slot.LabelKey))
                    {
                        var localized = _translationService[slot.LabelKey];
                        if (!IsMissingLocalization(localized))
                            slot.Label = localized;
                    }

                    if (TryPickCultureString(slot.Labels, culture, out var slotLabel))
                        slot.Label = slotLabel;
                }
            }
        }

        TemplateKeyboardActionResolver.Apply(template);

        return template;
    }

    public GameProfileTemplate LoadDefaultTemplate() => LoadTemplate(DefaultProfileId);

    public TemplateOption? ReloadTemplates(string? preferredProfileId = null)
    {
        AvailableTemplates.Clear();
        var templatesDir = LoadTemplateDirectory();
        if (!Directory.Exists(templatesDir))
        {
            ProfilesLoaded?.Invoke(this, EventArgs.Empty);
            return null;
        }

        var jsonFiles = _fileSystem.GetFiles(templatesDir, "*.json", SearchOption.TopDirectoryOnly);
        var options = new List<TemplateOption>();
        foreach (var file in jsonFiles)
        {
            var profileId = Path.GetFileNameWithoutExtension(file);
            try
            {
                var template = LoadTemplate(profileId);
                options.Add(new TemplateOption
                {
                    ProfileId = profileId,
                    TemplateGroupId = template.TemplateGroupId,
                    DisplayName = string.IsNullOrWhiteSpace(template.DisplayName) ? profileId : template.DisplayName,
                    RadialMenus = template.RadialMenus?.ToList()
                });
            }
            catch
            {
                // Ignore invalid templates while loading the list.
            }
        }

        foreach (var option in options.OrderBy(o => o.DisplayName, StringComparer.OrdinalIgnoreCase))
            AvailableTemplates.Add(option);

        ProfilesLoaded?.Invoke(this, EventArgs.Empty);
        return SelectTemplate(preferredProfileId);
    }

    public TemplateOption? SelectTemplate(string? preferredProfileId = null)
    {
        return
            (preferredProfileId is not null
                ? AvailableTemplates.FirstOrDefault(t =>
                    string.Equals(t.ProfileId, preferredProfileId, StringComparison.OrdinalIgnoreCase))
                : null) ??
            AvailableTemplates.FirstOrDefault(t => t.ProfileId == DefaultProfileId) ??
            AvailableTemplates.FirstOrDefault(t => t.TemplateGroupId == DefaultProfileId) ??
            AvailableTemplates.FirstOrDefault();
    }

    public GameProfileTemplate? LoadSelectedTemplate(TemplateOption? selectedTemplate)
    {
        if (selectedTemplate is null)
            return null;

        return LoadTemplate(selectedTemplate.ProfileId);
    }

    public bool TemplateExists(string profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId)) return false;
        var templatePath = Path.Combine(LoadTemplateDirectory(), $"{profileId.Trim()}.json");
        return _fileSystem.FileExists(templatePath);
    }

    /// <summary>
    /// Heuristic: profiles whose <see cref="GameProfileTemplate.TemplateGroupId"/> values match or look like variants
    /// (<c>mygame</c> and <c>mygame-battle</c>) typically target the same executable; missing <c>targetProcessName</c>
    /// on the variant should inherit from the sibling the user already configured.
    /// </summary>
    public static bool ProfilesLikelyShareGameExecutable(string? previousTemplateGroupId, string? newTemplateGroupId)
    {
        if (string.IsNullOrWhiteSpace(previousTemplateGroupId) || string.IsNullOrWhiteSpace(newTemplateGroupId))
            return false;

        var a = previousTemplateGroupId.Trim();
        var b = newTemplateGroupId.Trim();
        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
            return true;

        static bool IsSegmentPrefix(string shorter, string longer) =>
            longer.StartsWith(shorter + "-", StringComparison.OrdinalIgnoreCase)
            || longer.StartsWith(shorter + ".", StringComparison.OrdinalIgnoreCase);

        return a.Length <= b.Length ? IsSegmentPrefix(a, b) : IsSegmentPrefix(b, a);
    }

    public static string EnsureValidTemplateGroupId(string templateGroupId)
    {
        var normalized = (templateGroupId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ArgumentException("Template group ID (templateGroupId) is required.", nameof(templateGroupId));

        if (!ValidIdPattern.IsMatch(normalized))
            throw new ArgumentException("Template group ID (templateGroupId) can only contain letters, digits, dot, underscore, and dash.", nameof(templateGroupId));

        return normalized;
    }

    public string CreateUniqueProfileId(string templateGroupId, string? displayName)
    {
        var normalizedTemplateGroupId = EnsureValidTemplateGroupId(templateGroupId);
        var displaySegment = SlugSegment(displayName);
        var baseId = string.IsNullOrWhiteSpace(displaySegment)
            ? normalizedTemplateGroupId
            : $"{normalizedTemplateGroupId}__{displaySegment}";

        if (!TemplateExists(baseId))
        {
            return baseId;
        }

        var index = 2;
        while (true)
        {
            var candidate = $"{baseId}-{index}";
            if (!TemplateExists(candidate))
            {
                return candidate;
            }
            index++;
        }
    }

    public void SaveTemplate(GameProfileTemplate template, bool allowOverwrite = true)
    {
        if (template is null) throw new ArgumentNullException(nameof(template));

        var validation = ValidateTemplate(template);
        if (!validation.IsValid)
            throw new InvalidOperationException($"Cannot save profile with errors: {string.Join(", ", validation.Errors)}");

        var templatesDir = LoadTemplateDirectory();
        _fileSystem.CreateDirectory(templatesDir);

        if (template.SchemaVersion <= 0)
            template.SchemaVersion = 1;

        template.TemplateGroupId = EnsureValidTemplateGroupId(template.TemplateGroupId);
        template.DisplayName ??= string.Empty;
        template.Mappings ??= new System.Collections.Generic.List<MappingEntry>();

        if (string.IsNullOrWhiteSpace(template.ProfileId))
            template.ProfileId = CreateUniqueProfileId(template.TemplateGroupId, template.DisplayName);
        else
            template.ProfileId = EnsureValidProfileId(template.ProfileId);

        var templatePath = Path.Combine(templatesDir, $"{template.ProfileId}.json");
        if (!allowOverwrite && _fileSystem.FileExists(templatePath))
            throw new InvalidOperationException($"A profile with id '{template.ProfileId}' already exists.");

        var json = JsonConvert.SerializeObject(template, Formatting.Indented);
        _fileSystem.WriteAllText(templatePath, json, Encoding.UTF8);
    }

    public void DeleteTemplate(string profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId)) return;
        var templatePath = Path.Combine(LoadTemplateDirectory(), $"{profileId.Trim()}.json");

        if (_fileSystem.FileExists(templatePath))
            _fileSystem.DeleteFile(templatePath);
    }

    public IValidationResult ValidateTemplate(GameProfileTemplate template)
    {
        var validator = new ProfileValidator();
        return validator.Validate(template);
    }

    private static string EnsureValidProfileId(string profileId)
    {
        var normalized = (profileId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ArgumentException("Profile id is required.", nameof(profileId));

        if (!ValidIdPattern.IsMatch(normalized))
            throw new ArgumentException("Profile id contains invalid characters.", nameof(profileId));

        return normalized;
    }

    private static string SlugSegment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var chars = value.Trim()
            .ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray();

        var collapsed = MyRegex().Replace(new string(chars), "-").Trim('-');
        return string.IsNullOrWhiteSpace(collapsed) ? string.Empty : collapsed;
    }

    [GeneratedRegex("-{2,}")]
    private static partial Regex MyRegex();

    private static bool IsMissingLocalization(string value)
        => value.Length >= 2 && value[0] == '[' && value[^1] == ']';

    /// <summary>Resolves <paramref name="map"/> using <paramref name="culture"/> and its parent chain; keys are matched case-insensitively.</summary>
    private static bool TryPickCultureString(
        IReadOnlyDictionary<string, string>? map,
        CultureInfo culture,
        out string value)
    {
        value = string.Empty;
        if (map is null || map.Count == 0)
            return false;

        for (var c = culture; c is not null && !string.IsNullOrEmpty(c.Name); c = c.Parent)
        {
            if (TryGetCultureMapValue(map, c.Name, out value))
                return true;
        }

        return TryGetCultureMapValue(map, "default", out value);
    }

    private static bool TryGetCultureMapValue(
        IReadOnlyDictionary<string, string> map,
        string key,
        out string value)
    {
        value = string.Empty;
        if (string.IsNullOrWhiteSpace(key))
            return false;

        foreach (var kv in map)
        {
            if (string.IsNullOrWhiteSpace(kv.Value))
                continue;
            if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = kv.Value;
                return true;
            }
        }

        return false;
    }
}

