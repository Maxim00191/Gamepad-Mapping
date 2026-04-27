#nullable enable
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Gamepad_Mapping.Utils;

/// <summary>
/// Debug-only workspace integration tracing. Call sites are omitted in non-DEBUG builds via <see cref="ConditionalAttribute"/>.
/// Uses <see cref="App.Logger"/> at <c>Debug</c> level; <see cref="GamepadMapperGUI.Services.Infrastructure.FileLogger"/> does not persist debug lines in release.
/// </summary>
internal static class WorkspaceDebugTrace
{
    [Conditional("DEBUG")]
    public static void Log(
        string category,
        string message,
        [CallerMemberName] string caller = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        var name = System.IO.Path.GetFileName(file);
        App.Logger.Debug($"[{category}] {name}:{line} {caller} — {message}");
    }
}
