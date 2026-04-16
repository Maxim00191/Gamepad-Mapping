using System.Windows;
using Gamepad_Mapping.ViewModels;
using GamepadMapperGUI.Services.Infrastructure;

namespace Gamepad_Mapping.Views;

public partial class CommunityTemplateUploadWindow
{
    public CommunityTemplateUploadWindow()
    {
        InitializeComponent();
    }

    private void Upload_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not CommunityTemplateUploadDialogViewModel vm)
            return;

        if (!vm.TryCommit(out var err))
        {
            var loc = Application.Current?.Resources["Loc"] as TranslationService;
            var message = err ?? loc?["CommunityUpload_Error_InvalidInput"] ?? "Invalid input.";
            var title = loc?["CommunityUpload_ButtonUpload"] ?? "Upload";
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
        Close();
    }
}
