using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Hardcodet.Wpf.TaskbarNotification;
using Gamepad_Mapping.ViewModels;

namespace GamepadMapperGUI.Services.Infrastructure;

/// <summary>
/// Hosts a Windows notification-area icon (via <see cref="TaskbarIcon"/>) with open/exit actions while the main window can be hidden.
/// </summary>
public sealed class TrayNotifyIconService : IDisposable
{
    private readonly Window _window;
    private readonly MainViewModel _viewModel;
    private TaskbarIcon? _taskbarIcon;
    private bool _disposed;

    public TrayNotifyIconService(Window window, MainViewModel viewModel)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    /// <summary>Creates the tray icon on first use.</summary>
    public void EnsureIconCreatedAndVisible()
    {
        if (_disposed)
            return;

        if (_taskbarIcon is null)
            CreateTaskbarIcon();
    }

    private void CreateTaskbarIcon()
    {
        var tooltip = AppUiLocalization.GetString("TrayIcon_Tooltip");
        if (tooltip.Length > 63)
            tooltip = tooltip[..63];

        _taskbarIcon = new TaskbarIcon
        {
            IconSource = LoadTrayImageSource(),
            ToolTipText = tooltip
        };

        var menu = new ContextMenu();
        ApplyTrayMenuResources(menu);

        var openItem = new MenuItem();
        var exitItem = new MenuItem();
        var separator = new Separator();

        ApplyTrayMenuItemStyle(openItem);
        ApplyTrayMenuItemStyle(exitItem);
        ApplyTraySeparatorStyle(separator);

        RefreshTrayMenuHeaders(openItem, exitItem);
        menu.Items.Add(openItem);
        menu.Items.Add(separator);
        menu.Items.Add(exitItem);

        menu.Opened += (_, _) => RefreshTrayMenuHeaders(openItem, exitItem);

        openItem.Click += (_, _) => RunOnUiThread(RestoreMainWindow);
        exitItem.Click += (_, _) => RunOnUiThread(ExitFromTray);

        _taskbarIcon.ContextMenu = menu;
        _taskbarIcon.TrayMouseDoubleClick += (_, _) => RunOnUiThread(RestoreMainWindow);
    }

    private static void ApplyTrayMenuResources(ContextMenu menu)
    {
        var app = Application.Current;
        if (app is null)
            return;

        if (app.TryFindResource("TrayNotifyContextMenuStyle") is Style menuStyle)
            menu.Style = menuStyle;
    }

    private static void ApplyTrayMenuItemStyle(MenuItem item)
    {
        if (Application.Current?.TryFindResource("TrayNotifyMenuItemStyle") is Style style)
            item.Style = style;
    }

    private static void ApplyTraySeparatorStyle(Separator separator)
    {
        if (Application.Current?.TryFindResource("TrayNotifyMenuSeparatorStyle") is Style style)
            separator.Style = style;
    }

    private static void RefreshTrayMenuHeaders(MenuItem openItem, MenuItem exitItem)
    {
        openItem.Header = AppUiLocalization.GetString("TrayMenu_Open");
        exitItem.Header = AppUiLocalization.GetString("TrayMenu_Exit");
    }

    private static ImageSource LoadTrayImageSource()
    {
        var uri = new Uri("pack://application:,,,/Assets/Icons/Gamepad%20Mapping.ico");
        return BitmapFrame.Create(uri, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
    }

    private void RunOnUiThread(Action action)
    {
        if (_window.Dispatcher.CheckAccess())
            action();
        else
            _window.Dispatcher.Invoke(action);
    }

    private void RestoreMainWindow()
    {
        _window.ShowInTaskbar = true;
        _window.Show();
        _window.WindowState = WindowState.Normal;
        _window.Activate();
        _viewModel.OnMainWindowRestoredFromTray();
    }

    private void ExitFromTray()
    {
        if (!_viewModel.TryPrepareShutdownAfterWorkspacePrompt())
            return;

        _viewModel.BeginApplicationShutdown();
        Application.Current?.Shutdown();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        _taskbarIcon?.Dispose();
        _taskbarIcon = null;
    }
}
