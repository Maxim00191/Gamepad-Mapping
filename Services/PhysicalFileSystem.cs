using System.IO;
using System.Text;
using GamepadMapperGUI.Interfaces.Services;

namespace GamepadMapperGUI.Services;

public class PhysicalFileSystem : IFileSystem
{
    public bool FileExists(string path) => File.Exists(path);
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);
    public string ReadAllText(string path, Encoding encoding) => File.ReadAllText(path, encoding);
    public void WriteAllText(string path, string contents, Encoding encoding) => File.WriteAllText(path, contents, encoding);
    public void CopyFile(string sourcePath, string destinationPath, bool overwrite) => File.Copy(sourcePath, destinationPath, overwrite);
    public void DeleteFile(string path) => File.Delete(path);
    public string[] GetFiles(string path, string searchPattern, SearchOption searchOption) => Directory.GetFiles(path, searchPattern, searchOption);
    public string[] GetDirectories(string path) => Directory.GetDirectories(path);
    public string? GetDirectoryName(string path) => Path.GetDirectoryName(path);
}
