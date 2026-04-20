using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using GamepadMapperGUI.Interfaces.Core;
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

namespace GamepadMapperGUI.Services.Storage;

public partial class ProfileService : IProfileService
{
    private static readonly Regex ValidIdPattern = new("^[a-zA-Z0-9][a-zA-Z0-9._-]*$", RegexOptions.Compiled);

    private readonly Dictionary<string, TemplateStorageLocation> _templateResolveIndex = new(StringComparer.OrdinalIgnoreCase);

    private readonly ISettingsService _settingsService;
    private readonly TranslationService _translationService;
    private readonly IFileSystem _fileSystem;
    private readonly IPathProvider _pathProvider;
    private readonly AppSettings _settings;

    public ProfileService(
        ISettingsService? settingsService = null,
        TranslationService? translationService = null,
        IFileSystem? fileSystem = null,
        IPathProvider? pathProvider = null,
        AppSettings? appSettings = null)
    {
        _fileSystem = fileSystem ?? new PhysicalFileSystem();
        _pathProvider = pathProvider ?? new AppPathProvider();
        _settingsService = settingsService ?? new SettingsService(_fileSystem, _pathProvider);
        _translationService = translationService
            ?? Application.Current?.Resources["Loc"] as TranslationService
            ?? new TranslationService();
        _settings = appSettings ?? _settingsService.LoadSettings();
    }

    public string DefaultProfileId => _settings.DefaultProfileId;

    public string? LastSelectedTemplateProfileId => _settings.LastSelectedTemplateProfileId;

    public void PersistLastSelectedTemplateProfileId(string? storageKey)
    {
        _settings.LastSelectedTemplateProfileId = string.IsNullOrWhiteSpace(storageKey) ? null : storageKey.Trim();
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

    public GameProfileTemplate LoadTemplate(string profileIdOrStorageKey)
    {
        if (string.IsNullOrWhiteSpace(profileIdOrStorageKey))
            throw new ArgumentException("Profile id is required.", nameof(profileIdOrStorageKey));

        if (!TryResolveTemplateLocation(profileIdOrStorageKey.Trim(), out var loc))
            throw new FileNotFoundException($"Template not found: {profileIdOrStorageKey}");

        var templatePath = AppPaths.TemplateCatalogPaths.GetTemplateJsonPath(
            LoadTemplateDirectory(), loc.CatalogSubfolder, loc.FileStem);

        if (!_fileSystem.FileExists(templatePath))
            throw new FileNotFoundException($"Template not found: {templatePath}", templatePath);

        return LoadTemplateFromPath(templatePath, loc.CatalogSubfolder, loc.FileStem);
    }

    public GameProfileTemplate LoadDefaultTemplate() => LoadTemplate(DefaultProfileId);

    public TemplateOption? ReloadTemplates(string? preferredProfileIdOrStorageKey = null)
    {
        AvailableTemplates.Clear();
        _templateResolveIndex.Clear();
        var templatesDir = LoadTemplateDirectory();
        if (!_fileSystem.DirectoryExists(templatesDir))
        {
            ProfilesLoaded?.Invoke(this, EventArgs.Empty);
            return null;
        }

        var pending = new List<(TemplateOption Option, GameProfileTemplate Template, string Stem, string? Folder)>();
        foreach (var (fullPath, catalogFolder, stem) in EnumerateTemplateCatalogJsonFiles(templatesDir))
        {
            try
            {
                var template = LoadTemplateFromPath(fullPath, catalogFolder, stem);
                _templateResolveIndex[TemplateStorageKey.Format(catalogFolder, stem)] =
                    new TemplateStorageLocation(catalogFolder, stem);

                var logicalId = template.ProfileId?.Trim();
                if (!string.IsNullOrEmpty(logicalId))
                    _templateResolveIndex[logicalId] = new TemplateStorageLocation(catalogFolder, stem);

                var baselineTitle = string.IsNullOrWhiteSpace(template.DisplayName) ? stem : template.DisplayName.Trim();
                var opt = new TemplateOption
                {
                    ProfileId = stem,
                    CatalogSubfolder = catalogFolder,
                    TemplateGroupId = template.EffectiveTemplateGroupId,
                    DisplayNameBaseline = baselineTitle,
                    DisplayNames = template.DisplayNames,
                    DisplayNameKey = template.DisplayNameKey ?? string.Empty,
                    CatalogFolderDisplayNames = template.TemplateCatalogFolderNames,
                    Author = (template.Author ?? string.Empty).Trim(),
                    RadialMenus = template.RadialMenus?.ToList()
                };
                CatalogDescriptionLocalizer.ApplyTemplateOption(opt, _translationService);
                pending.Add((opt, template, stem, catalogFolder));
            }
            catch
            {
                // Ignore invalid templates while loading the list.
            }
        }

        var stemCounts = pending
            .GroupBy(x => x.Stem, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        foreach (var x in pending)
        {
            if (stemCounts[x.Stem] == 1)
                _templateResolveIndex[x.Stem] = new TemplateStorageLocation(x.Folder, x.Stem);
        }

        foreach (var opt in pending.Select(p => p.Option).OrderBy(o => o.ResolvedDisplayName, StringComparer.OrdinalIgnoreCase))
            AvailableTemplates.Add(opt);

        ProfilesLoaded?.Invoke(this, EventArgs.Empty);
        return SelectTemplate(preferredProfileIdOrStorageKey);
    }

    public TemplateOption? SelectTemplate(string? preferredProfileIdOrStorageKey = null)
    {
        var p = (preferredProfileIdOrStorageKey ?? string.Empty).Trim();
        if (p.Length > 0)
        {
            var exact = AvailableTemplates
                .FirstOrDefault(t => string.Equals(t.StorageKey, p, StringComparison.OrdinalIgnoreCase));
            if (exact is not null)
                return exact;

            if (TryResolveTemplateLocation(p, out var loc))
            {
                var fromIndex = AvailableTemplates.FirstOrDefault(o => o.MatchesLocation(loc));
                if (fromIndex is not null)
                    return fromIndex;
            }

            var sameStem = AvailableTemplates
                .Where(t => string.Equals(t.ProfileId, p, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (sameStem.Count == 1)
                return sameStem[0];
        }

        return
            AvailableTemplates.FirstOrDefault(t =>
                string.Equals(t.ProfileId, DefaultProfileId, StringComparison.OrdinalIgnoreCase)) ??
            AvailableTemplates.FirstOrDefault(t =>
                string.Equals(t.TemplateGroupId, DefaultProfileId, StringComparison.OrdinalIgnoreCase)) ??
            AvailableTemplates.FirstOrDefault();
    }

    public GameProfileTemplate? LoadSelectedTemplate(TemplateOption? selectedTemplate)
    {
        if (selectedTemplate is null)
            return null;

        var path = AppPaths.TemplateCatalogPaths.GetTemplateJsonPath(
            LoadTemplateDirectory(),
            selectedTemplate.CatalogSubfolder,
            selectedTemplate.ProfileId);

        if (!_fileSystem.FileExists(path))
            return null;

        return LoadTemplateFromPath(path, selectedTemplate.CatalogSubfolder, selectedTemplate.ProfileId);
    }

    public bool TryResolveTemplateLocation(string requestedId, out TemplateStorageLocation location)
    {
        location = default;
        var id = (requestedId ?? string.Empty).Trim();
        if (id.Length == 0)
            return false;

        if (_templateResolveIndex.TryGetValue(id, out location))
            return true;

        if (!AppPaths.TemplateCatalogPaths.TryParseStorageKey(id, out var folder, out var stem))
            return false;

        var loc = new TemplateStorageLocation(folder, stem);
        if (_templateResolveIndex.TryGetValue(loc.StorageKey, out location))
            return true;

        return false;
    }

    public bool TemplateExists(string profileIdOrStorageKey)
        => TryResolveTemplateLocation(profileIdOrStorageKey ?? string.Empty, out _);

    /// <summary>
    /// Heuristic: profiles whose effective template group ids (<see cref="GameProfileTemplate.EffectiveTemplateGroupId"/>) match or look like variants
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

    public static bool IsValidId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return false;
        return ValidIdPattern.IsMatch(id.Trim());
    }

    public static string EnsureValidTemplateGroupId(string templateGroupId)
    {
        var normalized = (templateGroupId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ArgumentException("Template group ID (templateGroupId) is required.", nameof(templateGroupId));

        if (!IsValidId(normalized))
            throw new ArgumentException("Template group ID (templateGroupId) can only contain letters, digits, dot, underscore, and dash.", nameof(templateGroupId));

        return normalized;
    }

    public string CreateUniqueProfileId(string templateGroupId, string? displayName, string? catalogFolder = null)
    {
        var normalizedTemplateGroupId = EnsureValidTemplateGroupId(templateGroupId);
        var displaySegment = SlugSegment(displayName);
        var baseId = string.IsNullOrWhiteSpace(displaySegment)
            ? normalizedTemplateGroupId
            : $"{normalizedTemplateGroupId}__{displaySegment}";

        if (!TemplateJsonExistsInCatalog(catalogFolder, baseId))
            return baseId;

        var index = 2;
        while (true)
        {
            var candidate = $"{baseId}-{index}";
            if (!TemplateJsonExistsInCatalog(catalogFolder, candidate))
                return candidate;
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

        template.DisplayName ??= string.Empty;
        template.Mappings ??= new List<MappingEntry>();

        var normalizedFolder = AppPaths.TemplateCatalogPaths.NormalizeCatalogSubfolderForSave(template.TemplateCatalogFolder ?? string.Empty);
        template.TemplateCatalogFolder = string.IsNullOrEmpty(normalizedFolder) ? null : normalizedFolder;

        if (string.IsNullOrWhiteSpace(template.ProfileId))
        {
            if (string.IsNullOrWhiteSpace(template.TemplateGroupId))
                throw new InvalidOperationException("Cannot allocate a profile id without templateGroupId when profileId is empty.");
            template.ProfileId = CreateUniqueProfileId(
                EnsureValidTemplateGroupId(template.TemplateGroupId),
                template.DisplayName,
                template.TemplateCatalogFolder);
        }
        else
        {
            template.ProfileId = EnsureValidProfileId(template.ProfileId);
        }

        var g = (template.TemplateGroupId ?? string.Empty).Trim();
        if (g.Length == 0 || string.Equals(g, template.ProfileId, StringComparison.OrdinalIgnoreCase))
            template.TemplateGroupId = null;
        else
            template.TemplateGroupId = EnsureValidTemplateGroupId(g);

        var templatePath = AppPaths.TemplateCatalogPaths.GetTemplateJsonPath(
            templatesDir, template.TemplateCatalogFolder, template.ProfileId);

        var parentDir = Path.GetDirectoryName(templatePath)!;
        _fileSystem.CreateDirectory(parentDir);

        if (!allowOverwrite && _fileSystem.FileExists(templatePath))
            throw new InvalidOperationException($"A profile with id '{template.ProfileId}' already exists.");

        var json = JsonConvert.SerializeObject(template, Formatting.Indented);
        _fileSystem.WriteAllText(templatePath, json, Encoding.UTF8);
    }

    public void DeleteTemplate(string profileIdOrStorageKey)
    {
        var id = (profileIdOrStorageKey ?? string.Empty).Trim();
        if (id.Length == 0) return;

        if (TryResolveTemplateLocation(id, out var loc))
        {
            DeleteFileAtLocation(loc);
            return;
        }

        if (AppPaths.TemplateCatalogPaths.TryParseStorageKey(id, out var folder, out var stem))
            DeleteFileAtLocation(new TemplateStorageLocation(folder, stem));
    }

    public IValidationResult ValidateTemplate(GameProfileTemplate template)
    {
        var validator = new ProfileValidator();
        return validator.Validate(template);
    }

    public static string EnsureValidProfileId(string profileId)
    {
        var normalized = (profileId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ArgumentException("Profile id is required.", nameof(profileId));

        if (!ValidIdPattern.IsMatch(normalized))
            throw new ArgumentException("Profile id contains invalid characters.", nameof(profileId));

        return normalized;
    }

    private void DeleteFileAtLocation(TemplateStorageLocation loc)
    {
        var templatePath = AppPaths.TemplateCatalogPaths.GetTemplateJsonPath(
            LoadTemplateDirectory(), loc.CatalogSubfolder, loc.FileStem);

        if (_fileSystem.FileExists(templatePath))
            _fileSystem.DeleteFile(templatePath);
    }

    private bool TemplateJsonExistsInCatalog(string? catalogFolder, string fileStem)
    {
        var path = AppPaths.TemplateCatalogPaths.GetTemplateJsonPath(LoadTemplateDirectory(), catalogFolder, fileStem);
        return _fileSystem.FileExists(path);
    }

    private IEnumerable<(string fullPath, string? catalogFolder, string stem)> EnumerateTemplateCatalogJsonFiles(string templatesDir)
    {
        foreach (var file in _fileSystem.GetFiles(templatesDir, "*.json", SearchOption.AllDirectories))
        {
            if (string.Equals(Path.GetFileName(file), "index.json", StringComparison.OrdinalIgnoreCase))
                continue;

            var rel = Path.GetRelativePath(templatesDir, file);
            var relDir = Path.GetDirectoryName(rel);
            string? catalogFolder = string.IsNullOrEmpty(relDir)
                ? null
                : relDir.Replace(Path.DirectorySeparatorChar, TemplateStorageKey.Separator)
                    .Replace(Path.AltDirectorySeparatorChar, TemplateStorageKey.Separator);
            yield return (file, catalogFolder, Path.GetFileNameWithoutExtension(file));
        }
    }

    private GameProfileTemplate LoadTemplateFromPath(string templatePath, string? catalogFolderFromDisk, string fileStemFromDisk)
    {
        var json = _fileSystem.ReadAllText(templatePath, Encoding.UTF8);
        var template = JsonConvert.DeserializeObject<GameProfileTemplate>(json);
        if (template is null)
            throw new InvalidOperationException($"Failed to parse template JSON: {templatePath}");

        template.TemplateCatalogFolder = string.IsNullOrEmpty(catalogFolderFromDisk) ? null : catalogFolderFromDisk;

        if (string.IsNullOrWhiteSpace(template.ProfileId))
            template.ProfileId = fileStemFromDisk;

        CatalogDescriptionLocalizer.ApplyLoadedTemplate(template, _translationService);

        TemplateKeyboardActionResolver.Apply(template);

        return template;
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


