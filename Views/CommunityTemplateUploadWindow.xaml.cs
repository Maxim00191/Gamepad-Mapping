using System.Windows;
using Gamepad_Mapping.ViewModels;

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
            MessageBox.Show(err ?? "Invalid input.", "Upload", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
        Close();
    }
}
