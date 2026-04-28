#nullable enable

using GamepadMapperGUI.Models.Automation;
using GamepadMapperGUI.Utils;

namespace GamepadMapping.Tests.Utils;

public sealed class AutomationYoloOnnxPathsTests
{
    [Fact]
    public void TryResolveEffectiveModelPath_returns_explicit_file_when_present()
    {
        var temp = Path.Combine(Path.GetTempPath(), $"gm-yolo-{Guid.NewGuid():N}.onnx");
        File.WriteAllText(temp, "");
        try
        {
            Assert.True(AutomationYoloOnnxPaths.TryResolveEffectiveModelPath(temp, out var resolved));
            Assert.Equal(temp, resolved);
        }
        finally
        {
            TryDelete(temp);
        }
    }

    [Fact]
    public void TryResolveEffectiveModelPath_scans_defaults_when_explicit_missing()
    {
        var dir = AutomationYoloOnnxPaths.GetUserModelsDirectory();
        var marker = Path.Combine(dir, AutomationYoloOnnxInferenceDefaults.DefaultModelFileNames[0]);
        var created = false;
        try
        {
            if (!File.Exists(marker))
            {
                File.WriteAllText(marker, "");
                created = true;
            }

            Assert.True(
                AutomationYoloOnnxPaths.TryResolveEffectiveModelPath("__nonexistent__.onnx", out var resolved));
            Assert.Equal(marker, resolved);
        }
        finally
        {
            if (created)
                TryDelete(marker);
        }
    }

    [Fact]
    public void TryResolveEffectiveModelPath_resolves_relative_content_root_path()
    {
        Assert.True(
            AutomationYoloOnnxPaths.TryResolveEffectiveModelPath(
                AutomationYoloOnnxPaths.DefaultBundledModelRelativePath,
                out var resolved));
        Assert.Equal(AutomationYoloOnnxPaths.GetBundledDefaultModelPath(), resolved);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // ignore test cleanup failures
        }
    }
}
