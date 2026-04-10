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
    var handshakeName = ResolveHandshakeName(args);
    if (!File.Exists(planPath))
        throw new FileNotFoundException("Install plan not found.", planPath);

    var json = File.ReadAllText(planPath);
    var plan = JsonSerializer.Deserialize<InstallExecutionPlanDto>(json);
    if (plan is null)
        throw new InvalidOperationException("Failed to parse install plan.");

    // Validate once before acknowledging startup, so launcher can keep app alive on invalid/stale plans.
    UpdaterInstallPlanValidator.Validate(plan);
    SignalHandshake(handshakeName);

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

static string? ResolveHandshakeName(string[] args)
{
    for (var i = 0; i < args.Length; i++)
    {
        if (string.Equals(args[i], "--handshake", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            return args[i + 1];
    }

    return null;
}

static string? ResolveAppDisplayName(string[] args)
{
    for (var i = 0; i < args.Length; i++)
    {
        if (string.Equals(args[i], "--plan", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
        {
            var planPath = args[i + 1];
            if (File.Exists(planPath))
            {
                try
                {
                    var json = File.ReadAllText(planPath);
                    var plan = JsonSerializer.Deserialize<InstallExecutionPlanDto>(json);
                    return plan?.AppDisplayName;
                }
                catch { /* Best effort */ }
            }
        }
    }

    return null;
}

static void SignalHandshake(string? handshakeName)
{
    if (string.IsNullOrWhiteSpace(handshakeName))
        return;

    try
    {
        if (Mutex.TryOpenExisting(handshakeName, out var mutex))
        {
            // Releasing the mutex signals the waiting process that we are ready.
            mutex.ReleaseMutex();
            mutex.Dispose();
        }
    }
    catch
    {
        // Handshake signaling is best-effort; if it fails, the launcher will timeout
        // or detect process exit.
    }
}

static void RelaunchFromTemp(string[] originalArgs)
{
    var currentExePath = Environment.ProcessPath;
    if (string.IsNullOrWhiteSpace(currentExePath))
        throw new InvalidOperationException("Cannot resolve updater executable path.");

    var sourceDir = Path.GetDirectoryName(currentExePath)
        ?? throw new InvalidOperationException("Cannot resolve updater executable directory.");
    
    var appDisplayName = ResolveAppDisplayName(originalArgs) ?? "GenericApp";
    var tempDir = Path.Combine(Path.GetTempPath(), $"{appDisplayName}-Updater-{Guid.NewGuid():N}");
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

    // The bootstrap process should wait for the relocated process to signal the handshake
    // before exiting, to ensure the launcher doesn't see a premature exit.
    var handshakeName = ResolveHandshakeName(originalArgs);
    if (!string.IsNullOrWhiteSpace(handshakeName))
    {
        try
        {
            if (Mutex.TryOpenExisting(handshakeName, out var mutex))
            {
                // Wait for the relocated process to release the mutex.
                // We use a timeout to avoid hanging if the relocated process fails.
                mutex.WaitOne(TimeSpan.FromSeconds(15));
                mutex.ReleaseMutex(); // Release it again so the launcher can see it.
                mutex.Dispose();
            }
        }
        catch
        {
            // Best effort.
        }
    }
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
