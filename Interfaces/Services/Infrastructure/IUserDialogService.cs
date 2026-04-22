using System.Windows;

namespace GamepadMapperGUI.Interfaces.Services.Infrastructure;

public interface IUserDialogService
{
    MessageBoxResult Show(
        string message,
        string title,
        MessageBoxButton buttons,
        MessageBoxImage image,
        MessageBoxResult defaultResult = MessageBoxResult.None,
        Window? owner = null);
}
