using System.Windows;
using System.Windows.Media;

namespace GamepadMapperGUI.Interfaces.Services.Infrastructure;

public interface IWindowTitleBarThemeService
{
    bool TryApply(Window window, Color backgroundColor, Color foregroundColor, Color borderColor, bool usesLightTheme);
}
