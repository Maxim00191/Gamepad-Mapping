using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Storage;
using GamepadMapperGUI.Interfaces.Services.Update;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Interfaces.Services.Radial;

namespace GamepadMapping.Tests.Mocks;

public class MockFileSystem : IFileSystem
{
    public Dictionary<string, string> Files { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> Directories { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool FileExists(string path) => Files.ContainsKey(path);
    public bool DirectoryExists(string path) => Directories.Contains(path);
    public void CreateDirectory(string path) => Directories.Add(path);
    public string ReadAllText(string path, Encoding encoding)
    {
        if (!Files.TryGetValue(path, out var content))
            throw new FileNotFoundException("File not found", path);
        return content;
    }
    public void WriteAllText(string path, string contents, Encoding encoding) => Files[path] = contents;
    public void CopyFile(string sourcePath, string destinationPath, bool overwrite)
    {
        if (!Files.TryGetValue(sourcePath, out var content))
            throw new FileNotFoundException("Source file not found", sourcePath);
        if (!overwrite && Files.ContainsKey(destinationPath))
            throw new IOException("Destination file already exists");
        Files[destinationPath] = content;
    }
    public void DeleteFile(string path) => Files.Remove(path);

    public string[] GetFiles(string path, string searchPattern, SearchOption searchOption)
    {
        var normalized = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var result = new List<string>();
        foreach (var key in Files.Keys)
        {
            var dir = Path.GetDirectoryName(key);
            if (dir is null)
                continue;

            if (searchOption == SearchOption.TopDirectoryOnly)
            {
                if (!string.Equals(dir, normalized, StringComparison.OrdinalIgnoreCase))
                    continue;
            }
            else if (!dir.StartsWith(normalized, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!SearchPatternMatches(Path.GetFileName(key), searchPattern))
                continue;

            result.Add(key);
        }

        return result.ToArray();
    }

    public string[] GetDirectories(string path)
    {
        var normalized = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var prefix = normalized + Path.DirectorySeparatorChar;
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var d in Directories)
        {
            if (!d.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || d.Length <= prefix.Length)
                continue;

            var tail = d[prefix.Length..];
            var sep = tail.AsSpan().IndexOfAny('\\', '/');
            var first = sep < 0 ? tail : tail[..sep];
            names.Add(Path.Combine(normalized, first));
        }

        foreach (var key in Files.Keys)
        {
            var dir = Path.GetDirectoryName(key);
            if (dir is null || dir.Length <= normalized.Length)
                continue;

            if (!dir.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            var tail = dir[prefix.Length..];
            var sep = tail.AsSpan().IndexOfAny('\\', '/');
            var first = sep < 0 ? tail : tail[..sep];
            names.Add(Path.Combine(normalized, first));
        }

        return names.ToArray();
    }

    private static bool SearchPatternMatches(string fileName, string searchPattern)
    {
        if (string.Equals(searchPattern, "*.json", StringComparison.OrdinalIgnoreCase))
            return fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase);

        return true;
    }

    public string? GetDirectoryName(string path) => Path.GetDirectoryName(path);
}

public class MockPathProvider : IPathProvider
{
    public string ContentRoot { get; set; } = @"C:\MockRoot";
    public string GetContentRoot() => ContentRoot;
}

