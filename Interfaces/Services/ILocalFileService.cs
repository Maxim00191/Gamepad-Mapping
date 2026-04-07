namespace GamepadMapperGUI.Interfaces.Services;

public interface ILocalFileService
{
    bool FileExists(string path);
    string ReadAllText(string path);
    void EnsureDirectory(string? directoryPath);
    void DeleteFileIfExists(string? path);
}
