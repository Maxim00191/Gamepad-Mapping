using System.Windows;

namespace Gamepad_Mapping;

public partial class MainWindow : Window
{
    private readonly ViewModels.MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        // Force standard resizable window chrome at runtime in case any
        // theme/style initialization overrides XAML window settings.
        WindowStyle = WindowStyle.SingleBorderWindow;
        ResizeMode = ResizeMode.CanResize;
        SizeToContent = SizeToContent.Manual;
        WindowState = WindowState.Normal;
        _viewModel = new ViewModels.MainViewModel();
        DataContext = _viewModel;
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Dispose();
        base.OnClosed(e);
    }
}