using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Gamepad_Mapping.ViewModels;
using GamepadMapperGUI.Services.Infrastructure;

namespace Gamepad_Mapping.Views;

public partial class CommunityTemplateUploadWindow
{
    private bool _uploadCommitInFlight;

    public CommunityTemplateUploadWindow()
    {
        InitializeComponent();
    }

    private async void Upload_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not CommunityTemplateUploadDialogViewModel vm)
            return;

        if (_uploadCommitInFlight)
            return;

        _uploadCommitInFlight = true;

        var loc = Application.Current?.Resources["Loc"] as TranslationService;
        var title = loc?["CommunityUpload_ButtonUpload"] ?? "Upload";

        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            var (ok, err) = await vm.TryCommitAsync(CancellationToken.None).ConfigureAwait(true);
            if (!ok)
            {
                var message = err ?? loc?["CommunityUpload_Error_InvalidInput"] ?? "Invalid input.";
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }
        catch (TaskCanceledException)
        {
            return;
        }
        finally
        {
            Mouse.OverrideCursor = null;
            _uploadCommitInFlight = false;
        }

        DialogResult = true;
        Close();
    }
}
