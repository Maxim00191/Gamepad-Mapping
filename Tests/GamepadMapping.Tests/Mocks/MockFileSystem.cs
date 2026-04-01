using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GamepadMapperGUI.Interfaces.Services;

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
        var result = new List<string>();
        foreach (var key in Files.Keys)
        {
            if (key.StartsWith(path, StringComparison.OrdinalIgnoreCase))
            {
                result.Add(key);
            }
        }
        return result.ToArray();
    }
    public string GetDirectoryName(string path) => Path.GetDirectoryName(path);
}

public class MockPathProvider : IPathProvider
{
    public string ContentRoot { get; set; } = @"C:\MockRoot";
    public string GetContentRoot() => ContentRoot;
}
