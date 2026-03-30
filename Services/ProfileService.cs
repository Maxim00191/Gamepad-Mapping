using System;
using System.Text;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using GamepadMapperGUI.Models;
using Newtonsoft.Json;

namespace GamepadMapperGUI.Services;

public partial class ProfileService
{
    private static readonly Regex ValidIdPattern = new("^[a-zA-Z0-9][a-zA-Z0-9._-]*$", RegexOptions.Compiled);
    private readonly SettingsService _settingsService;
    private readonly AppSettings _settings;

    public ProfileService()
    {
        _settingsService = new SettingsService();
        _settings = SettingsService.LoadSettings();
    }

    public string DefaultGameId => _settings.DefaultGameId;

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

        return template;
    }

    public GameProfileTemplate LoadDefaultTemplate() => LoadTemplate(DefaultGameId);

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
}

