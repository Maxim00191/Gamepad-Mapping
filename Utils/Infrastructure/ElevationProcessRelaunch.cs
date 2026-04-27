using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;

namespace Gamepad_Mapping.Utils;

/// <summary>
/// Relaunches the current process elevated, preserving the same command line and working directory as the running instance.
/// </summary>
public static class ElevationProcessRelaunch
{
    /// <summary>
    /// Starts an elevated copy of the current process (UAC). Returns true if a new process was started; false if the user cancelled UAC or startup failed.
    /// </summary>
    public static bool TryRelaunchCurrentProcessAsAdministrator()
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath))
            return false;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = BuildRelaunchArguments(Environment.GetCommandLineArgs()),
                WorkingDirectory = Environment.CurrentDirectory,
                UseShellExecute = true,
                Verb = "runas"
            };
            using var p = Process.Start(psi);
            return p is not null;
        }
        catch (Win32Exception)
        {
            // User cancelled the UAC prompt.
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Builds the argument string for <see cref="ProcessStartInfo.Arguments"/> from a full argv array (including argv[0] = executable path).
    /// </summary>
    internal static string BuildRelaunchArguments(IReadOnlyList<string> commandLineArgs)
    {
        if (commandLineArgs is null || commandLineArgs.Count <= 1)
            return string.Empty;

        return string.Join(" ", commandLineArgs.Skip(1).Select(QuoteIfNeeded));
    }

    private static string QuoteIfNeeded(string arg)
    {
        if (string.IsNullOrEmpty(arg))
            return "\"\"";

        if (arg.IndexOfAny(new[] { ' ', '\t', '"' }) < 0)
            return arg;

        return "\"" + arg.Replace("\"", "\\\"") + "\"";
    }
}
