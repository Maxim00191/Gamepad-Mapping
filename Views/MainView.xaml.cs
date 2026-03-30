using System.Windows.Controls;
using System.Windows.Input;
using System.ComponentModel;
using System.Windows.Threading;
using Gamepad_Mapping.ViewModels;

namespace Gamepad_Mapping.Views;

public partial class MainView : UserControl
{
    private MainViewModel? _vm;

    public MainView()
    {
        InitializeComponent();
        PreviewKeyDown += MainView_PreviewKeyDown;
        Loaded += MainView_Loaded;
        Unloaded += MainView_Unloaded;
    }

    private void MainView_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        // DataContext is assigned by MainWindow after InitializeComponent, so hook up on Loaded.
        _vm = DataContext as MainViewModel;
        if (_vm is null) return;
        _vm.PropertyChanged += VmOnPropertyChanged;
    }

    private void MainView_Unloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_vm is null) return;
        _vm.PropertyChanged -= VmOnPropertyChanged;
        _vm = null;
    }

    private void VmOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.IsRecordingKeyboardKey))
            return;

        if (_vm is null || !_vm.IsRecordingKeyboardKey)
            return;

        // Move keyboard focus onto this control so we reliably receive the next key press.
        Dispatcher.BeginInvoke(new System.Action(() =>
        {
            Focus();
            Keyboard.Focus(this);
        }), DispatcherPriority.Input);
    }

    private void MainView_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
            return;

        if (vm.IsRecordingKeyboardKey && e.Key == Key.Escape)
        {
            vm.CancelKeyboardKeyRecording();
            e.Handled = true;
            return;
        }

        if (vm.TryCaptureKeyboardKey(e.Key, e.SystemKey))
            e.Handled = true;
    }
}

