using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;

namespace Updater.Install;

internal static class UpdaterInstallFileOps
{
    public static void CopyAndVerifyZipSnapshot(string sourceZipPath, string trustedZipPath, string expectedSha256)
    {
        using var source = File.Open(sourceZipPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var destination = File.Open(trustedZipPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var sha256 = SHA256.Create();
        var buffer = new byte[1024 * 1024];
        while (true)
        {
            var read = source.Read(buffer, 0, buffer.Length);
            if (read <= 0)
                break;
            destination.Write(buffer, 0, read);
            sha256.TransformBlock(buffer, 0, read, null, 0);
        }

        sha256.TransformFinalBlock([], 0, 0);
        var actual = Convert.ToHexString(sha256.Hash!).ToLowerInvariant();
        if (!string.Equals(actual, expectedSha256.Trim().ToLowerInvariant(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"SHA-256 mismatch. Expected={expectedSha256} Actual={actual}");
    }

    public static string ExtractPackageRoot(string trustedZipPath, string stagingRoot)
    {
        Directory.CreateDirectory(stagingRoot);
        using var stream = File.Open(trustedZipPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        var extractedRoot = Path.Combine(stagingRoot, "_extract");
        Directory.CreateDirectory(extractedRoot);

        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.FullName))
                continue;

            var entryPath = entry.FullName.Replace('/', '\\');
            var destinationPath = Path.GetFullPath(Path.Combine(extractedRoot, entryPath));
            if (!UpdaterInstallPathPolicy.IsSubPathOf(destinationPath, extractedRoot))
                throw new InvalidOperationException($"Blocked archive traversal entry: {entry.FullName}");

            if (entry.FullName.EndsWith("/", StringComparison.Ordinal))
            {
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            var dir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            using var input = entry.Open();
            using var output = File.Open(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
            input.CopyTo(output);
        }

        return ResolveSourceRoot(extractedRoot);
    }

    public static void BackupPreservedData(string targetRoot, string preserveBackupRoot, IReadOnlyList<string> preservePaths)
    {
        if (preservePaths.Count == 0)
            return;
        Directory.CreateDirectory(preserveBackupRoot);
        foreach (var relative in preservePaths)
        {
            var sourcePath = Path.Combine(targetRoot, relative);
            if (File.Exists(sourcePath))
            {
                var backupPath = Path.Combine(preserveBackupRoot, relative);
                var backupDir = Path.GetDirectoryName(backupPath);
                if (!string.IsNullOrWhiteSpace(backupDir))
                    Directory.CreateDirectory(backupDir);
                File.Copy(sourcePath, backupPath, overwrite: true);
                continue;
            }

            if (Directory.Exists(sourcePath))
            {
                var backupDirPath = Path.Combine(preserveBackupRoot, relative);
                CopyDirectoryContents(sourcePath, backupDirPath, overwriteFiles: true);
            }
        }
    }

    public static void RestorePreservedData(string preserveBackupRoot, string stagingRoot)
    {
        if (!Directory.Exists(preserveBackupRoot))
            return;
        CopyDirectoryContents(preserveBackupRoot, stagingRoot, overwriteFiles: true);
    }

    public static void CopyDirectoryContents(string sourceRoot, string destinationRoot, bool overwriteFiles)
    {
        Directory.CreateDirectory(destinationRoot);
        foreach (var directory in Directory.EnumerateDirectories(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceRoot, directory);
            Directory.CreateDirectory(Path.Combine(destinationRoot, relative));
        }

        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceRoot, file);
            var destination = Path.Combine(destinationRoot, relative);
            var destinationDir = Path.GetDirectoryName(destination);
            if (!string.IsNullOrWhiteSpace(destinationDir))
                Directory.CreateDirectory(destinationDir);
            File.Copy(file, destination, overwrite: overwriteFiles);
        }
    }

    public static void MergeConfigFiles(string currentRoot, string stagingRoot, InstallLogger logger)
    {
        foreach (var relative in UpdaterInstallPathPolicy.MergeTargetRelativePaths)
        {
            var currentPath = Path.Combine(currentRoot, relative);
            var stagingPath = Path.Combine(stagingRoot, relative);
            if (!File.Exists(currentPath) || !File.Exists(stagingPath))
                continue;

            try
            {
                var packageNode = JsonNode.Parse(File.ReadAllText(stagingPath, Encoding.UTF8));
                var localNode = JsonNode.Parse(File.ReadAllText(currentPath, Encoding.UTF8));
                if (packageNode is not JsonObject packageObject || localNode is not JsonObject localObject)
                    continue;

                var merged = MergeObjects(packageObject, localObject, relative, logger, "$");
                File.WriteAllText(stagingPath, merged.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
                logger.Info($"Merged config file: {relative}");
            }
            catch (Exception ex)
            {
                logger.Error($"Config merge failed, keeping package version: {relative}. {ex.Message}");
            }
        }
    }

    public static void WriteUpdateSecurityState(string targetDir, string? trustedReleaseTag, InstallLogger logger)
    {
        if (string.IsNullOrWhiteSpace(trustedReleaseTag))
            return;

        try
        {
            var updatesDir = Path.Combine(targetDir, "Updates");
            Directory.CreateDirectory(updatesDir);
            var statePath = Path.Combine(updatesDir, "update-security-state.json");
            var state = new
            {
                HighestTrustedReleaseTag = trustedReleaseTag.Trim(),
                UpdatedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(statePath, json, new UTF8Encoding(false));
            logger.Info($"Updated trusted release baseline: {trustedReleaseTag}");
        }
        catch (Exception ex)
        {
            logger.Error($"Failed to persist trusted release baseline: {ex.Message}");
        }
    }

    public static void SafeDeleteDirectory(string path, InstallLogger logger)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch (Exception ex)
        {
            logger.Error($"Cleanup directory failed: {path}. {ex.Message}");
        }
    }

    public static void SafeDeleteFile(string path, InstallLogger logger)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            logger.Error($"Cleanup file failed: {path}. {ex.Message}");
        }
    }

    public static void MoveDirectoryWithRetry(
        string sourcePath,
        string destinationPath,
        InstallLogger logger,
        string operationName,
        int maxAttempts = 8,
        int initialDelayMs = 120)
    {
        if (maxAttempts <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), "maxAttempts must be greater than 0.");

        var delayMs = Math.Max(1, initialDelayMs);
        Exception? lastError = null;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                Directory.Move(sourcePath, destinationPath);
                if (attempt > 1)
                    logger.Info($"{operationName} succeeded on attempt {attempt}/{maxAttempts}.");
                return;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                lastError = ex;
                if (attempt >= maxAttempts)
                    break;

                logger.Error($"{operationName} failed on attempt {attempt}/{maxAttempts}: {ex.Message}. Retrying after {delayMs}ms.");
                Thread.Sleep(delayMs);
                delayMs = Math.Min(delayMs * 2, 1500);
            }
        }

        throw new IOException($"{operationName} failed after {maxAttempts} attempts.", lastError);
    }

    private static string ResolveSourceRoot(string extractedRoot)
    {
        var rootItems = Directory.GetFileSystemEntries(extractedRoot);
        if (rootItems.Length == 1 && Directory.Exists(rootItems[0]))
            return rootItems[0];
        return extractedRoot;
    }

    private static JsonObject MergeObjects(
        JsonObject packageObject,
        JsonObject localObject,
        string configRelativePath,
        InstallLogger logger,
        string jsonPath)
    {
        var result = new JsonObject();
        foreach (var kv in packageObject)
            result[kv.Key] = kv.Value?.DeepClone();

        foreach (var kv in localObject)
        {
            var keyPath = $"{jsonPath}.{kv.Key}";
            if (!result.ContainsKey(kv.Key))
            {
                result[kv.Key] = kv.Value?.DeepClone();
                continue;
            }

            var packageValue = result[kv.Key];
            var localValue = kv.Value;
            if (!AreJsonNodeTypesCompatible(packageValue, localValue))
            {
                logger.Error($"Config merge type conflict at {configRelativePath}:{keyPath}. Keeping package value to avoid schema corruption.");
                continue;
            }

            if (packageValue is JsonObject packageChild && localValue is JsonObject localChild)
            {
                result[kv.Key] = MergeObjects(packageChild, localChild, configRelativePath, logger, keyPath);
                continue;
            }

            result[kv.Key] = kv.Value?.DeepClone();
        }

        return result;
    }

    private static bool AreJsonNodeTypesCompatible(JsonNode? packageValue, JsonNode? localValue)
    {
        if (packageValue is null || localValue is null)
            return true;
        return GetJsonNodeKind(packageValue) == GetJsonNodeKind(localValue);
    }

    private static JsonNodeKind GetJsonNodeKind(JsonNode node) =>
        node switch
        {
            JsonObject => JsonNodeKind.Object,
            JsonArray => JsonNodeKind.Array,
            JsonValue value => ClassifyJsonValue(value),
            _ => JsonNodeKind.Unknown
        };

    private static JsonNodeKind ClassifyJsonValue(JsonValue value)
    {
        var element = value.GetValue<JsonElement>();
        return element.ValueKind switch
        {
            JsonValueKind.String => JsonNodeKind.String,
            JsonValueKind.Number => JsonNodeKind.Number,
            JsonValueKind.True or JsonValueKind.False => JsonNodeKind.Boolean,
            JsonValueKind.Null => JsonNodeKind.Null,
            _ => JsonNodeKind.Unknown
        };
    }

    private enum JsonNodeKind
    {
        Unknown = 0,
        Object = 1,
        Array = 2,
        String = 3,
        Number = 4,
        Boolean = 5,
        Null = 6
    }
}
