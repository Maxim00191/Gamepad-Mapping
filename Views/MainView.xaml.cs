using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Gamepad_Mapping.ViewModels;

namespace Gamepad_Mapping.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
        Loaded += (_, _) => TryUpdateRightPanelLayout();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        Unhook(e.OldValue as MainViewModel);
        Hook(e.NewValue as MainViewModel);
        TryUpdateRightPanelLayout();
    }

    private void Hook(MainViewModel? vm)
    {
        if (vm is null) return;
        vm.MappingEditorPanel.PropertyChanged += OnRightPanelChildPropertyChanged;
        vm.GamepadMonitorPanel.PropertyChanged += OnRightPanelChildPropertyChanged;
    }

    private void Unhook(MainViewModel? vm)
    {
        if (vm is null) return;
        vm.MappingEditorPanel.PropertyChanged -= OnRightPanelChildPropertyChanged;
        vm.GamepadMonitorPanel.PropertyChanged -= OnRightPanelChildPropertyChanged;
    }

    private void OnRightPanelChildPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MappingEditorViewModel.IsMappingDetailsExpanderExpanded)
            && e.PropertyName != nameof(GamepadMonitorViewModel.IsMonitorExpanderExpanded))
            return;

        if (Dispatcher.CheckAccess())
            TryUpdateRightPanelLayout();
        else
            Dispatcher.BeginInvoke(TryUpdateRightPanelLayout);
    }

    private void TryUpdateRightPanelLayout()
    {
        if (DataContext is not MainViewModel vm)
            return;

        bool details = vm.MappingEditorPanel.IsMappingDetailsExpanderExpanded;
        bool monitor = vm.GamepadMonitorPanel.IsMonitorExpanderExpanded;

        var r0 = RightPanelGrid.RowDefinitions[0];
        var r1 = RightPanelGrid.RowDefinitions[1];
        var r2 = RightPanelGrid.RowDefinitions[2];

        if (details && monitor)
        {
            r0.Height = new GridLength(3, GridUnitType.Star);
            r0.MinHeight = 100;
            r1.Height = new GridLength(6);
            r2.Height = new GridLength(2, GridUnitType.Star);
            r2.MinHeight = 80;
            RightPanelRowSplitter.Visibility = Visibility.Visible;
        }
        else if (details)
        {
            r0.Height = new GridLength(1, GridUnitType.Star);
            r0.MinHeight = 100;
            r1.Height = new GridLength(0);
            r2.Height = GridLength.Auto;
            r2.MinHeight = 0;
            RightPanelRowSplitter.Visibility = Visibility.Collapsed;
        }
        else if (monitor)
        {
            r0.Height = GridLength.Auto;
            r0.MinHeight = 0;
            r1.Height = new GridLength(0);
            r2.Height = new GridLength(1, GridUnitType.Star);
            r2.MinHeight = 80;
            RightPanelRowSplitter.Visibility = Visibility.Collapsed;
        }
        else
        {
            r0.Height = GridLength.Auto;
            r0.MinHeight = 0;
            r1.Height = new GridLength(0);
            r2.Height = GridLength.Auto;
            r2.MinHeight = 0;
            RightPanelRowSplitter.Visibility = Visibility.Collapsed;
        }
    }
}
