#nullable enable

using System.Diagnostics.CodeAnalysis;
using System.IO;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Utils;

public static class AutomationYoloOnnxPaths
{
    private const string UserRelativeFolderName = "YoloOnnx";
    public const string DefaultBundledModelRelativePath = "Assets/Automation/Models/yolo.onnx";

    public static string GetBundledModelsDirectory() =>
        Path.Combine(AppPaths.ResolveContentRoot(), "Assets", "Automation", "Models");

    public static string GetBundledDefaultModelPath() =>
        Path.Combine(AppPaths.ResolveContentRoot(), "Assets", "Automation", "Models", "yolo.onnx");

    public static string GetUserModelsDirectory()
    {
        var root = AppPaths.GetAutomationWorkspaceStorageDirectory();
        var dir = Path.Combine(root, UserRelativeFolderName);
        try
        {
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            return dir;
        }
        catch
        {
            return dir;
        }
    }

    public static bool TryResolveEffectiveModelPath(
        string? explicitPath,
        [NotNullWhen(true)] out string? resolvedExistingPath)
    {
        var trimmed = explicitPath?.Trim();
        if (!string.IsNullOrEmpty(trimmed))
        {
            try
            {
                if (TryResolveExplicitPath(trimmed, out var explicitResolved))
                {
                    resolvedExistingPath = explicitResolved;
                    return true;
                }
            }
            catch
            {
                resolvedExistingPath = null;
                return false;
            }
        }

        foreach (var root in GetSearchRootsOrdered())
        {
            foreach (var name in AutomationYoloOnnxInferenceDefaults.DefaultModelFileNames)
            {
                try
                {
                    var candidate = Path.Combine(root, name);
                    if (File.Exists(candidate))
                    {
                        resolvedExistingPath = candidate;
                        return true;
                    }
                }
                catch
                {
                    // ignore candidate
                }
            }
        }

        resolvedExistingPath = null;
        return false;
    }

    private static IEnumerable<string> GetSearchRootsOrdered()
    {
        yield return GetUserModelsDirectory();
        yield return GetBundledModelsDirectory();
    }

    private static bool TryResolveExplicitPath(string explicitPath, [NotNullWhen(true)] out string? resolvedPath)
    {
        if (File.Exists(explicitPath))
        {
            resolvedPath = Path.GetFullPath(explicitPath);
            return true;
        }

        if (Path.IsPathRooted(explicitPath))
        {
            resolvedPath = null;
            return false;
        }

        var normalized = explicitPath
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
        var rootedFromContent = Path.Combine(AppPaths.ResolveContentRoot(), normalized);
        if (File.Exists(rootedFromContent))
        {
            resolvedPath = rootedFromContent;
            return true;
        }

        resolvedPath = null;
        return false;
    }
}
