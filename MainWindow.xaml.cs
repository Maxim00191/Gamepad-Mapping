using System.Windows;
using System.Reflection;
using Gamepad_Mapping.ViewModels;

namespace Gamepad_Mapping;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        // Force standard resizable window chrome at runtime in case any
        // theme/style initialization overrides XAML window settings.
        WindowStyle = WindowStyle.SingleBorderWindow;
        ResizeMode = ResizeMode.CanResize;
        SizeToContent = SizeToContent.Manual;
        WindowState = WindowState.Normal;
        _viewModel = viewModel;
        DataContext = _viewModel;
        Title = $"Gamepad Mapping v{GetDisplayVersion()} - Maxim";
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

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Dispose();
        base.OnClosed(e);
    }
}