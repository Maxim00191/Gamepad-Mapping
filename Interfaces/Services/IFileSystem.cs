using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GamepadMapperGUI.Interfaces.Services;

public interface IFileSystem
{
    bool FileExists(string path);
    bool DirectoryExists(string path);
    void CreateDirectory(string path);
    string ReadAllText(string path, Encoding encoding);
    void WriteAllText(string path, string contents, Encoding encoding);
    void CopyFile(string sourcePath, string destinationPath, bool overwrite);
    void DeleteFile(string path);
    string[] GetFiles(string path, string searchPattern, SearchOption searchOption);
    string[] GetDirectories(string path);
    string? GetDirectoryName(string path);
}
