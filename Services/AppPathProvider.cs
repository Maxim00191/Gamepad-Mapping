using GamepadMapperGUI.Interfaces.Services;
using GamepadMapperGUI.Utils;

namespace GamepadMapperGUI.Services;

public class AppPathProvider : IPathProvider
{
    public string GetContentRoot() => AppPaths.ResolveContentRoot();
}
