using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
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
    private readonly AppSettings _settings;

    public ProfileService(ISettingsService? settingsService = null, TranslationService? translationService = null)
    {
        _settingsService = settingsService ?? new SettingsService();
        _translationService = translationService
            ?? Application.Current?.Resources["Loc"] as TranslationService
            ?? new TranslationService();
        _settings = _settingsService.LoadSettings();
    }

    public string DefaultGameId => _settings.DefaultGameId;

    public int ModifierGraceMs => Math.Clamp(_settings.ModifierGraceMs, 50, 10_000);
    public ObservableCollection<TemplateOption> AvailableTemplates { get; } = [];
    public event EventHandler? ProfilesLoaded;

    public string LoadTemplateDirectory()
    {
        var root = AppPaths.ResolveContentRoot();
        var templatesDir = Path.Combine(root, _settings.TemplatesDirectory);
        Directory.CreateDirectory(templatesDir);
        return templatesDir;
    }

    public GameProfileTemplate LoadTemplate(string profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
            throw new ArgumentException("Profile id is required.", nameof(profileId));

        var templatePath = Path.Combine(LoadTemplateDirectory(), $"{profileId.Trim()}.json");
        if (!File.Exists(templatePath))
            throw new FileNotFoundException($"Template not found: {templatePath}", templatePath);

        var json = File.ReadAllText(templatePath, Encoding.UTF8);
        var template = JsonConvert.DeserializeObject<GameProfileTemplate>(json);
        if (template is null)
            throw new InvalidOperationException($"Failed to parse template JSON: {templatePath}");

        if (string.IsNullOrWhiteSpace(template.ProfileId))
            template.ProfileId = profileId.Trim();

        if (!string.IsNullOrWhiteSpace(template.DisplayNameKey))
        {
            var localized = _translationService[template.DisplayNameKey];
            if (!IsMissingLocalization(localized))
                template.DisplayName = localized;
        }

        foreach (var mapping in template.Mappings)
        {
            if (string.IsNullOrWhiteSpace(mapping.DescriptionKey))
                continue;

            var localized = _translationService[mapping.DescriptionKey];
            if (!IsMissingLocalization(localized))
                mapping.Description = localized;
        }

        return template;
    }

    public GameProfileTemplate LoadDefaultTemplate() => LoadTemplate(DefaultGameId);

    public TemplateOption? ReloadTemplates(string? preferredProfileId = null)
    {
        AvailableTemplates.Clear();
        var templatesDir = LoadTemplateDirectory();
        if (!Directory.Exists(templatesDir))
        {
            ProfilesLoaded?.Invoke(this, EventArgs.Empty);
            return null;
        }

        var jsonFiles = Directory.GetFiles(templatesDir, "*.json", SearchOption.TopDirectoryOnly);
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
                    GameId = template.GameId,
                    DisplayName = string.IsNullOrWhiteSpace(template.DisplayName) ? profileId : template.DisplayName
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
            (preferredProfileId is not null ? AvailableTemplates.FirstOrDefault(t => t.ProfileId == preferredProfileId) : null) ??
            AvailableTemplates.FirstOrDefault(t => t.ProfileId == DefaultGameId) ??
            AvailableTemplates.FirstOrDefault(t => t.GameId == DefaultGameId) ??
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
        return File.Exists(templatePath);
    }

    public static string EnsureValidGameId(string gameId)
    {
        var normalized = (gameId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ArgumentException("Game key is required.", nameof(gameId));

        if (!ValidIdPattern.IsMatch(normalized))
            throw new ArgumentException("Game key can only contain letters, digits, dot, underscore, and dash.", nameof(gameId));

        return normalized;
    }

    public string CreateUniqueProfileId(string gameId, string? displayName)
    {
        var normalizedGameId = EnsureValidGameId(gameId);
        var displaySegment = SlugSegment(displayName);
        var baseId = string.IsNullOrWhiteSpace(displaySegment)
            ? normalizedGameId
            : $"{normalizedGameId}__{displaySegment}";

        var candidate = baseId;
        var index = 2;
        while (TemplateExists(candidate))
        {
            candidate = $"{baseId}-{index}";
            index++;
        }

        return candidate;
    }

    public void SaveTemplate(GameProfileTemplate template, bool allowOverwrite = true)
    {
        if (template is null) throw new ArgumentNullException(nameof(template));

        var templatesDir = LoadTemplateDirectory();
        Directory.CreateDirectory(templatesDir);

        if (template.SchemaVersion <= 0)
            template.SchemaVersion = 1;

        template.GameId = EnsureValidGameId(template.GameId);
        template.DisplayName ??= string.Empty;
        template.Mappings ??= new System.Collections.Generic.List<MappingEntry>();

        if (string.IsNullOrWhiteSpace(template.ProfileId))
            template.ProfileId = CreateUniqueProfileId(template.GameId, template.DisplayName);
        else
            template.ProfileId = EnsureValidProfileId(template.ProfileId);

        var templatePath = Path.Combine(templatesDir, $"{template.ProfileId}.json");
        if (!allowOverwrite && File.Exists(templatePath))
            throw new InvalidOperationException($"A profile with id '{template.ProfileId}' already exists.");

        var json = JsonConvert.SerializeObject(template, Formatting.Indented);
        File.WriteAllText(templatePath, json, Encoding.UTF8);
    }

    public void DeleteTemplate(string profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId)) return;
        var templatePath = Path.Combine(LoadTemplateDirectory(), $"{profileId.Trim()}.json");

        if (File.Exists(templatePath))
            File.Delete(templatePath);
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
}

