using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Gamepad_Mapping.ViewModels;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Services.Infrastructure;

namespace Gamepad_Mapping.Views;

public partial class CommunityTemplateUploadWindow
{
    private bool _uploadCommitInFlight;
    private readonly IUserDialogService _userDialogService;

    public CommunityTemplateUploadWindow(IUserDialogService? userDialogService = null)
    {
        _userDialogService = userDialogService ?? new UserDialogService();
        InitializeComponent();
    }

    private async void Upload_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not CommunityTemplateUploadDialogViewModel vm)
            return;

        if (_uploadCommitInFlight)
            return;

        _uploadCommitInFlight = true;

        var title = AppUiLocalization.GetString("CommunityUpload_ButtonUpload");

        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            var (ok, err) = await vm.TryCommitAsync(CancellationToken.None);
            if (!ok)
            {
                var message = err ?? AppUiLocalization.GetString("CommunityUpload_Error_InvalidInput");
                _userDialogService.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }
        catch (TaskCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            Gamepad_Mapping.App.Logger.Warning($"Community upload validation failed: {ex.Message}");
            _userDialogService.Show(
                string.Format(AppUiLocalization.GetString("CommunityCatalog_StatusUploadFailed"), ex.Message),
                title,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
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
