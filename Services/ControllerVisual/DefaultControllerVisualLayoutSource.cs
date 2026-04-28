using System.Diagnostics;
using System.IO;
using Gamepad_Mapping.Interfaces.Services.ControllerVisual;
using GamepadMapperGUI.Models;
using Gamepad_Mapping.Utils.ControllerVisual;
using GamepadMapperGUI.Models.ControllerVisual;
using GamepadMapperGUI.Utils;

namespace Gamepad_Mapping.Services.ControllerVisual;

public sealed class DefaultControllerVisualLayoutSource : IControllerVisualLayoutSource
{
    private readonly Func<string?> _getActiveGamepadApiId;
    private readonly Func<string?, string> _normalizeGamepadApiId;
    private readonly Dictionary<string, ControllerVisualLayoutDescriptor> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly IReadOnlyDictionary<string, string> _layoutManifestByGamepadApiId;

    public DefaultControllerVisualLayoutSource(
        Func<string?>? getActiveGamepadApiId = null,
        Func<string?, string>? normalizeGamepadApiId = null,
        IReadOnlyDictionary<string, string>? layoutManifestByGamepadApiId = null)
    {
        _getActiveGamepadApiId = getActiveGamepadApiId ?? (() => GamepadSourceApiIds.XInput);
        _normalizeGamepadApiId = normalizeGamepadApiId ?? NormalizeApiId;
        _layoutManifestByGamepadApiId = layoutManifestByGamepadApiId ??
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [GamepadSourceApiIds.XInput] = ControllerSvgConstants.DefaultLayoutManifestFileName,
                [GamepadSourceApiIds.PlayStation] = ControllerSvgConstants.DualSenseLayoutManifestFileName
            };
    }

    public ControllerVisualLayoutDescriptor GetActiveLayout() =>
        GetLayoutForGamepadApi(_getActiveGamepadApiId());

    public ControllerVisualLayoutDescriptor GetLayoutForGamepadApi(string? gamepadApiId)
    {
        var normalizedApiId = _normalizeGamepadApiId(gamepadApiId);
        if (!_layoutManifestByGamepadApiId.TryGetValue(normalizedApiId, out var manifestFileName))
            manifestFileName = ControllerSvgConstants.DefaultLayoutManifestFileName;

        if (_cache.TryGetValue(manifestFileName, out var cached))
            return cached;

        var path = AppPaths.GetControllerVisualLayoutManifestPath(manifestFileName);
        if (File.Exists(path))
        {
            try
            {
                var json = File.ReadAllText(path);
                if (ControllerVisualManifestParser.TryParse(json, out var parsed) && parsed is not null)
                {
                    _cache[manifestFileName] = parsed;
                    return parsed;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to read controller layout manifest ({path}): {ex.Message}");
            }
        }
        else
            Debug.WriteLine($"Controller layout manifest not found: {path}");

        _cache[manifestFileName] = ControllerVisualLayoutFallbacks.Xbox;
        return ControllerVisualLayoutFallbacks.Xbox;
    }

    private static string NormalizeApiId(string? apiId)
    {
        if (string.IsNullOrWhiteSpace(apiId))
            return GamepadSourceApiIds.XInput;

        return apiId.Trim();
    }
}
