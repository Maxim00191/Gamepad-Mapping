using System.Diagnostics;
using System.IO;
using Gamepad_Mapping.Interfaces.Services;
using Gamepad_Mapping.Utils.ControllerSvg;
using GamepadMapperGUI.Models.ControllerVisual;
using GamepadMapperGUI.Utils;

namespace Gamepad_Mapping.Services;

public sealed class DefaultControllerVisualLayoutSource : IControllerVisualLayoutSource
{
    private readonly string _manifestFileName;
    private ControllerVisualLayoutDescriptor? _cached;

    public DefaultControllerVisualLayoutSource(string manifestFileName = ControllerSvgConstants.DefaultLayoutManifestFileName)
    {
        _manifestFileName = manifestFileName;
    }

    public ControllerVisualLayoutDescriptor GetActiveLayout()
    {
        if (_cached is not null) return _cached;

        var path = AppPaths.GetControllerVisualLayoutManifestPath(_manifestFileName);
        if (File.Exists(path))
        {
            try
            {
                var json = File.ReadAllText(path);
                if (ControllerVisualManifestParser.TryParse(json, out var parsed) && parsed is not null)
                {
                    _cached = parsed;
                    return _cached;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to read controller layout manifest ({path}): {ex.Message}");
            }
        }
        else
            Debug.WriteLine($"Controller layout manifest not found: {path}");

        _cached = ControllerVisualLayoutFallbacks.Xbox;
        return _cached;
    }
}
