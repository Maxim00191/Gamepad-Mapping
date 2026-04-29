#nullable enable

using System.Windows.Media.Imaging;

namespace GamepadMapperGUI.Interfaces.Services.Automation;

public interface INeedleBitmapCache
{
    BitmapSource? GetOrLoadExistingFile(string absolutePath);
}
