using System.IO;
using GamepadMapperGUI.Interfaces.Services;

namespace GamepadMapperGUI.Services;

public class LocalFileService : ILocalFileService
{
    public bool FileExists(string path) => File.Exists(path);

    public string ReadAllText(string path) => File.ReadAllText(path);

    public void EnsureDirectory(string? directoryPath)
    {
        if (!string.IsNullOrWhiteSpace(directoryPath))
            Directory.CreateDirectory(directoryPath);
    }

    public void DeleteFileIfExists(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        if (File.Exists(path))
            File.Delete(path);
    }
}
