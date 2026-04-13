using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Gamepad_Mapping.ViewModels;
using Gamepad_Mapping.Views.Sections;

namespace Gamepad_Mapping.Views;

public partial class MainView : UserControl
{
    private GamepadMonitorWindow? _monitorWindow;
    private GamepadMonitorViewModel? _hookedMonitor;
    private MainViewModel? _hookedMainVm;
    private bool _syncingMonitorWindowClosed;
    private bool _ensureMonitorWindowScheduled;

    public MainView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
        Unloaded += OnUnloaded;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_hookedMainVm is not null)
            _hookedMainVm.FocusMappingDetailsFirstFieldRequested -= OnFocusMappingDetailsFirstFieldRequested;
        _hookedMainVm = e.NewValue as MainViewModel;
        if (_hookedMainVm is not null)
            _hookedMainVm.FocusMappingDetailsFirstFieldRequested += OnFocusMappingDetailsFirstFieldRequested;
        MonitorVmHook();
    }

    private void OnFocusMappingDetailsFirstFieldRequested(object? sender, EventArgs e)
    {
        var panel = FindDescendant<MappingDetailsPanelView>(this);
        panel?.TryFocusFirstEditorField();
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
                return match;
            var nested = FindDescendant<T>(child);
            if (nested is not null)
                return nested;
        }

        return null;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => MonitorVmHook();

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_hookedMainVm is not null)
        {
            _hookedMainVm.FocusMappingDetailsFirstFieldRequested -= OnFocusMappingDetailsFirstFieldRequested;
            _hookedMainVm = null;
        }

        if (_hookedMonitor is not null)
        {
            _hookedMonitor.PropertyChanged -= OnMonitorVmPropertyChanged;
            _hookedMonitor = null;
        }

        if (_monitorWindow is not null)
        {
            _monitorWindow.Closed -= MonitorWindow_OnClosed;
            _monitorWindow.Close();
            _monitorWindow = null;
        }
    }

    private void MonitorVmHook()
    {
        if (DataContext is not MainViewModel vm) return;
        if (ReferenceEquals(_hookedMonitor, vm.GamepadMonitorPanel)) return;
        if (_hookedMonitor is not null)
            _hookedMonitor.PropertyChanged -= OnMonitorVmPropertyChanged;
        _hookedMonitor = vm.GamepadMonitorPanel;
        _hookedMonitor.PropertyChanged += OnMonitorVmPropertyChanged;
        ApplyMonitorWindowFromState();
    }

    private void OnMonitorVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(GamepadMonitorViewModel.IsMonitorExpanderExpanded))
            return;
        if (_syncingMonitorWindowClosed) return;
        ApplyMonitorWindowFromState();
    }

    private void ApplyMonitorWindowFromState()
    {
        if (_hookedMonitor is null) return;
        if (_hookedMonitor.IsMonitorExpanderExpanded)
            EnsureMonitorWindow();
        else
            CloseMonitorWindowInternal();
    }

    private void EnsureMonitorWindow()
    {
        if (DataContext is not MainViewModel vm) return;
        if (_monitorWindow is { IsLoaded: true })
        {
            _monitorWindow.Activate();
            return;
        }

        var owner = Window.GetWindow(this);
        if (owner is not null && !owner.IsVisible)
        {
            if (_ensureMonitorWindowScheduled) return;
            _ensureMonitorWindowScheduled = true;
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                _ensureMonitorWindowScheduled = false;
                if (_hookedMonitor?.IsMonitorExpanderExpanded != true) return;
                EnsureMonitorWindow();
            }, DispatcherPriority.ApplicationIdle);
            return;
        }

        _monitorWindow = new GamepadMonitorWindow
        {
            Owner = owner,
            DataContext = vm.GamepadMonitorPanel
        };
        _monitorWindow.Closed += MonitorWindow_OnClosed;
        _monitorWindow.Show();
    }

    private void CloseMonitorWindowInternal()
    {
        if (_monitorWindow is null) return;
        _monitorWindow.Closed -= MonitorWindow_OnClosed;
        _monitorWindow.Close();
        _monitorWindow = null;
    }

    private void MonitorWindow_OnClosed(object? sender, EventArgs e)
    {
        _monitorWindow = null;
        if (DataContext is not MainViewModel vm) return;
        _syncingMonitorWindowClosed = true;
        try
        {
            vm.GamepadMonitorPanel.IsMonitorExpanderExpanded = false;
        }
        finally
        {
            _syncingMonitorWindowClosed = false;
        }
    }
}
