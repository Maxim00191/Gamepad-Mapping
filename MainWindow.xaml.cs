using System.Windows;

namespace Gamepad_Mapping;

public partial class MainWindow : Window
{
    private readonly ViewModels.MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new ViewModels.MainViewModel();
        DataContext = _viewModel;
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Dispose();
        base.OnClosed(e);
    }
}