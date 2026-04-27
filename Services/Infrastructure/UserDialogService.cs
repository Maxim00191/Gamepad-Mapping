using System;
using System.Windows;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;

namespace GamepadMapperGUI.Services.Infrastructure;

public sealed class UserDialogService : IUserDialogService
{
    public MessageBoxResult Show(
        string message,
        string title,
        MessageBoxButton buttons,
        MessageBoxImage image,
        MessageBoxResult defaultResult = MessageBoxResult.None,
        Window? owner = null)
    {
        static MessageBoxResult ShowCore(
            Window? owner,
            string localMessage,
            string localTitle,
            MessageBoxButton localButtons,
            MessageBoxImage localImage,
            MessageBoxResult localDefaultResult)
            => owner is null
                ? MessageBox.Show(localMessage, localTitle, localButtons, localImage, localDefaultResult)
                : MessageBox.Show(owner, localMessage, localTitle, localButtons, localImage, localDefaultResult);

        Window? ResolveOwner(Window? localOwner) => localOwner ?? Application.Current?.MainWindow;

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
            return ShowCore(owner, message, title, buttons, image, defaultResult);

        if (dispatcher.CheckAccess())
            return ShowCore(ResolveOwner(owner), message, title, buttons, image, defaultResult);

        return dispatcher.Invoke(() =>
            ShowCore(ResolveOwner(owner), message, title, buttons, image, defaultResult));
    }

    public void ShowInfo(string message, string title, Window? owner = null)
        => Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information, owner: owner);

    public void ShowWarning(string message, string title, Window? owner = null)
        => Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning, owner: owner);

    public void ShowError(string message, string title, Window? owner = null)
        => Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error, owner: owner);

    public bool ConfirmYesNo(
        string message,
        string title,
        MessageBoxImage image = MessageBoxImage.Question,
        MessageBoxResult defaultResult = MessageBoxResult.None,
        Window? owner = null)
        => Show(message, title, MessageBoxButton.YesNo, image, defaultResult, owner) == MessageBoxResult.Yes;
}
