using System;
using System.IO;
using GamepadMapperGUI.Models.Core;

namespace GamepadMapperGUI.Core;

public static class AppInstallModeDetector
{
    public static AppInstallMode DetectCurrent()
    {
        try
        {
            var baseDir = AppContext.BaseDirectory;
            var appName = Path.GetFileNameWithoutExtension(Environment.ProcessPath) ?? "Gamepad Mapping";

            var runtimeConfigPath = Path.Combine(baseDir, $"{appName}.runtimeconfig.json");
            var appDllPath = Path.Combine(baseDir, $"{appName}.dll");

            var hasRuntimeConfig = File.Exists(runtimeConfigPath);
            var hasAppDll = File.Exists(appDllPath);

            if (hasRuntimeConfig || hasAppDll)
                return AppInstallMode.Fx;

            return AppInstallMode.Single;
        }
        catch
        {
            return AppInstallMode.Unknown;
        }
    }
}
