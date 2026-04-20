using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Reflection;
using Gamepad_Mapping.ViewModels;
using GamepadMapperGUI.Services.Infrastructure;

namespace Gamepad_Mapping;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        // Force standard resizable window chrome at runtime in case any
        // theme/style initialization overrides XAML window settings.
        WindowStyle = WindowStyle.SingleBorderWindow;
        ResizeMode = ResizeMode.CanResize;
        SizeToContent = SizeToContent.Manual;
        WindowState = WindowState.Normal;
        DataContext = _viewModel;
        Title = $"Gamepad Mapping v{GetDisplayVersion()} - Maxim";
    }

    private void MainWindow_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var mods = Keyboard.Modifiers;
        if (!mods.HasFlag(ModifierKeys.Control) || mods.HasFlag(ModifierKeys.Alt))
            return;

        if (Keyboard.FocusedElement is TextBoxBase)
            return;

        if (e.Key == Key.Z)
        {
            if (mods.HasFlag(ModifierKeys.Shift))
            {
                if (_viewModel.RuleClipboard.RedoWorkspaceEditCommand.CanExecute(null))
                {
                    _viewModel.RuleClipboard.RedoWorkspaceEditCommand.Execute(null);
                    e.Handled = true;
                }
            }
            else
            {
                if (_viewModel.RuleClipboard.UndoWorkspaceEditCommand.CanExecute(null))
                {
                    _viewModel.RuleClipboard.UndoWorkspaceEditCommand.Execute(null);
                    e.Handled = true;
                }
            }

            return;
        }

        if (mods.HasFlag(ModifierKeys.Shift))
            return;

        switch (e.Key)
        {
            case Key.A:
                if (_viewModel.RuleClipboard.SelectAllWorkspaceRulesCommand.CanExecute(null))
                {
                    _viewModel.RuleClipboard.SelectAllWorkspaceRulesCommand.Execute(null);
                    e.Handled = true;
                }

                break;
            case Key.C:
                if (_viewModel.RuleClipboard.CopyRuleCommand.CanExecute(null))
                {
                    _viewModel.RuleClipboard.CopyRuleCommand.Execute(null);
                    e.Handled = true;
                }

                break;
            case Key.V:
                if (_viewModel.RuleClipboard.PasteRuleCommand.CanExecute(null))
                {
                    _viewModel.RuleClipboard.PasteRuleCommand.Execute(null);
                    e.Handled = true;
                }

                break;
        }
    }

    private static string GetDisplayVersion()
    {
        var assembly = typeof(MainWindow).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            var plusIndex = informationalVersion.IndexOf('+');
            return plusIndex > 0 ? informationalVersion[..plusIndex] : informationalVersion;
        }

        var version = assembly.GetName().Version;
        return version is null ? "unknown" : version.ToString();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_viewModel.IsTemplateWorkspaceDirty)
        {
            var title = AppUiLocalization.GetString("WorkspaceUnsavedChangesTitle");
            var message = AppUiLocalization.GetString("WorkspaceUnsavedExitPrompt");
            var result = MessageBox.Show(message, title, MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
            switch (result)
            {
                case MessageBoxResult.Yes:
                    if (!_viewModel.TryPersistWorkspaceTemplateToDisk(out var err))
                    {
                        if (!string.IsNullOrWhiteSpace(err))
                        {
                            MessageBox.Show(
                                string.Format(AppUiLocalization.GetString("WorkspaceSave_FailedMessage"), err),
                                title,
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                        }

                        e.Cancel = true;
                    }

                    break;
                case MessageBoxResult.No:
                    break;
                default:
                    e.Cancel = true;
                    break;
            }
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Dispose();
        base.OnClosed(e);
    }
}