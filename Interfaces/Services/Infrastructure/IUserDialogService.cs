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

    void ShowInfo(string message, string title, Window? owner = null);

    void ShowWarning(string message, string title, Window? owner = null);

    void ShowError(string message, string title, Window? owner = null);

    bool ConfirmYesNo(
        string message,
        string title,
        MessageBoxImage image = MessageBoxImage.Question,
        MessageBoxResult defaultResult = MessageBoxResult.None,
        Window? owner = null);
}
