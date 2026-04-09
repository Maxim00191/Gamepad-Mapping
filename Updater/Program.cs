using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Updater.Install;

const int ExitGeneralFailure = 1;
const int ExitInvalidArguments = 2;
const int ExitPlanInvalid = 3;
const int ExitLockConflict = 4;

if (!args.Any(x => string.Equals(x, "--relocated", StringComparison.OrdinalIgnoreCase)))
{
    RelaunchFromTemp(args);
    return;
}

var exitCode = ExitGeneralFailure;
try
{
    var planPath = ResolvePlanPath(args);
    var ackPath = ResolveAckPath(args);
    if (!File.Exists(planPath))
        throw new FileNotFoundException("Install plan not found.", planPath);

    var json = File.ReadAllText(planPath);
    var plan = JsonSerializer.Deserialize<InstallExecutionPlanDto>(json);
    if (plan is null)
        throw new InvalidOperationException("Failed to parse install plan.");

    TryWriteAckFile(ackPath);

    var runner = new UpdaterInstallRunner();
    exitCode = runner.Run(plan);
}
catch (UpdaterInstallRunner.UpdateLockConflictException ex)
{
    Console.Error.WriteLine($"Updater lock conflict: {ex.Message}");
    exitCode = ExitLockConflict;
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine($"Updater invalid arguments: {ex.Message}");
    exitCode = ExitInvalidArguments;
}
catch (FileNotFoundException ex)
{
    Console.Error.WriteLine($"Updater plan missing: {ex.Message}");
    exitCode = ExitPlanInvalid;
}
catch (InvalidOperationException ex)
{
    Console.Error.WriteLine($"Updater invalid plan or state: {ex.Message}");
    exitCode = ExitPlanInvalid;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Updater failed: {ex.Message}");
    exitCode = ExitGeneralFailure;
}
finally
{
    Environment.Exit(exitCode);
}

static string ResolvePlanPath(string[] args)
{
    for (var i = 0; i < args.Length; i++)
    {
        if (string.Equals(args[i], "--plan", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            return args[i + 1];
    }

    throw new ArgumentException("Missing required argument: --plan <path>");
}

static string? ResolveAckPath(string[] args)
{
    for (var i = 0; i < args.Length; i++)
    {
        if (string.Equals(args[i], "--ack", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            return args[i + 1];
    }

    return null;
}

static void TryWriteAckFile(string? ackPath)
{
    if (string.IsNullOrWhiteSpace(ackPath))
        return;

    var fullPath = Path.GetFullPath(ackPath);
    var dir = Path.GetDirectoryName(fullPath);
    if (!string.IsNullOrWhiteSpace(dir))
        Directory.CreateDirectory(dir);
    File.WriteAllText(fullPath, DateTimeOffset.UtcNow.ToString("O"));
}

static void RelaunchFromTemp(string[] originalArgs)
{
    var currentExePath = Environment.ProcessPath;
    if (string.IsNullOrWhiteSpace(currentExePath))
        throw new InvalidOperationException("Cannot resolve updater executable path.");

    var sourceDir = Path.GetDirectoryName(currentExePath)
        ?? throw new InvalidOperationException("Cannot resolve updater executable directory.");
    var tempDir = Path.Combine(Path.GetTempPath(), $"GamepadMapping-Updater-{Guid.NewGuid():N}");
    Directory.CreateDirectory(tempDir);

    CopyIfExists(Path.Combine(sourceDir, "Updater.exe"), Path.Combine(tempDir, "Updater.exe"));
    CopyIfExists(Path.Combine(sourceDir, "Updater.dll"), Path.Combine(tempDir, "Updater.dll"));
    CopyIfExists(Path.Combine(sourceDir, "Updater.deps.json"), Path.Combine(tempDir, "Updater.deps.json"));
    CopyIfExists(Path.Combine(sourceDir, "Updater.runtimeconfig.json"), Path.Combine(tempDir, "Updater.runtimeconfig.json"));

    var tempExePath = Path.Combine(tempDir, "Updater.exe");
    if (!File.Exists(tempExePath))
        throw new FileNotFoundException("Temporary updater executable bootstrap failed.", tempExePath);

    var mergedArgs = originalArgs.Concat(["--relocated"]).ToArray();
    var psi = new ProcessStartInfo
    {
        FileName = tempExePath,
        Arguments = string.Join(" ", mergedArgs.Select(QuoteArgument)),
        UseShellExecute = false,
        WorkingDirectory = tempDir
    };

    using var process = Process.Start(psi);
    if (process is null)
        throw new InvalidOperationException("Failed to relaunch updater from temp directory.");
}

static void CopyIfExists(string sourcePath, string destinationPath)
{
    if (File.Exists(sourcePath))
        File.Copy(sourcePath, destinationPath, overwrite: true);
}

static string QuoteArgument(string arg)
{
    if (string.IsNullOrEmpty(arg))
        return "\"\"";
    return arg.Any(char.IsWhiteSpace) || arg.Contains('"')
        ? $"\"{arg.Replace("\"", "\\\"")}\""
        : arg;
}
