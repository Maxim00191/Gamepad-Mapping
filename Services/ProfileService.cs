using System;
using System.Text;
using System.IO;
using GamepadMapperGUI.Models;
using Newtonsoft.Json;

namespace GamepadMapperGUI.Services;

public class ProfileService
{
    private readonly SettingsService _settingsService;
    private readonly AppSettings _settings;

    public ProfileService()
    {
        _settingsService = new SettingsService();
        _settings = _settingsService.LoadSettings();
    }

    public string DefaultGameId => _settings.DefaultGameId;

    public string LoadTemplateDirectory()
    {
        var root = AppPaths.ResolveContentRoot();
        var templatesDir = Path.Combine(root, _settings.TemplatesDirectory);
        Directory.CreateDirectory(templatesDir);
        return templatesDir;
    }

    public GameProfileTemplate LoadTemplate(string gameId)
    {
        var root = AppPaths.ResolveContentRoot();
        var templatesDir = Path.Combine(root, _settings.TemplatesDirectory);
        Directory.CreateDirectory(templatesDir);

        var templatePath = Path.Combine(templatesDir, $"{gameId}.json");
        if (!File.Exists(templatePath))
            throw new FileNotFoundException($"Template not found: {templatePath}", templatePath);

        var json = File.ReadAllText(templatePath, Encoding.UTF8);
        var template = JsonConvert.DeserializeObject<GameProfileTemplate>(json);
        if (template is null)
            throw new InvalidOperationException($"Failed to parse template JSON: {templatePath}");

        return template;
    }

    public GameProfileTemplate LoadDefaultTemplate() => LoadTemplate(DefaultGameId);
}

