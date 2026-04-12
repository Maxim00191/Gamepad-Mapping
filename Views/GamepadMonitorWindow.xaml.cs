using System;
using System.Windows;
using Gamepad_Mapping.ViewModels;

namespace Gamepad_Mapping.Views;

public partial class GamepadMonitorWindow : Window
{
    public GamepadMonitorWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is GamepadMonitorViewModel vm)
            Width = GamepadMonitorViewModel.ClampMonitorWidth(vm.MonitorPanelWidth);
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (DataContext is not GamepadMonitorViewModel vm) return;
        if (!IsLoaded || WindowState == WindowState.Minimized) return;
        if (e.WidthChanged)
            vm.MonitorPanelWidth = GamepadMonitorViewModel.ClampMonitorWidth(ActualWidth);
    }
}
