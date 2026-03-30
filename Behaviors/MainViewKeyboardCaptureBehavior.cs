using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using GamepadMapperGUI.Interfaces.Services;
using Gamepad_Mapping.ViewModels;

namespace Gamepad_Mapping.Behaviors;

public static class MainViewKeyboardCaptureBehavior
{
    public static readonly DependencyProperty EnableKeyboardCaptureProperty =
        DependencyProperty.RegisterAttached(
            "EnableKeyboardCapture",
            typeof(bool),
            typeof(MainViewKeyboardCaptureBehavior),
            new PropertyMetadata(false, OnEnableKeyboardCaptureChanged));

    private static readonly DependencyProperty BehaviorStateProperty =
        DependencyProperty.RegisterAttached(
            "BehaviorState",
            typeof(BehaviorState),
            typeof(MainViewKeyboardCaptureBehavior),
            new PropertyMetadata(null));

    public static void SetEnableKeyboardCapture(DependencyObject element, bool value)
        => element.SetValue(EnableKeyboardCaptureProperty, value);

    public static bool GetEnableKeyboardCapture(DependencyObject element)
        => (bool)element.GetValue(EnableKeyboardCaptureProperty);

    private static void SetBehaviorState(DependencyObject element, BehaviorState? value)
        => element.SetValue(BehaviorStateProperty, value);

    private static BehaviorState? GetBehaviorState(DependencyObject element)
        => (BehaviorState?)element.GetValue(BehaviorStateProperty);

    private static void OnEnableKeyboardCaptureChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UserControl view)
            return;

        if ((bool)e.NewValue)
        {
            if (GetBehaviorState(view) is not null)
                return;

            var state = new BehaviorState(view);
            SetBehaviorState(view, state);
            state.Attach();
            return;
        }

        var existingState = GetBehaviorState(view);
        if (existingState is null)
            return;

        existingState.Detach();
        SetBehaviorState(view, null);
    }

    private sealed class BehaviorState(UserControl view)
    {
        private readonly UserControl _view = view;
        private MainViewModel? _viewModel;
        private IKeyboardCaptureService? _keyboardCaptureService;

        public void Attach()
        {
            _view.Loaded += OnLoaded;
            _view.Unloaded += OnUnloaded;
            _view.DataContextChanged += OnDataContextChanged;
            _view.PreviewKeyDown += OnPreviewKeyDown;
            BindFromDataContext();
        }

        public void Detach()
        {
            _view.Loaded -= OnLoaded;
            _view.Unloaded -= OnUnloaded;
            _view.DataContextChanged -= OnDataContextChanged;
            _view.PreviewKeyDown -= OnPreviewKeyDown;
            UnbindKeyboardCaptureService();
            _viewModel = null;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
            => BindFromDataContext();

        private void OnUnloaded(object sender, RoutedEventArgs e)
            => UnbindKeyboardCaptureService();

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
            => BindFromDataContext();

        private void BindFromDataContext()
        {
            if (_view.DataContext is not MainViewModel vm)
            {
                UnbindKeyboardCaptureService();
                _viewModel = null;
                return;
            }

            if (ReferenceEquals(_viewModel, vm) &&
                ReferenceEquals(_keyboardCaptureService, vm.KeyboardCaptureService))
            {
                return;
            }

            UnbindKeyboardCaptureService();
            _viewModel = vm;
            _keyboardCaptureService = vm.KeyboardCaptureService;
            _keyboardCaptureService.PropertyChanged += OnKeyboardCaptureServicePropertyChanged;
        }

        private void UnbindKeyboardCaptureService()
        {
            if (_keyboardCaptureService is not null)
                _keyboardCaptureService.PropertyChanged -= OnKeyboardCaptureServicePropertyChanged;

            _keyboardCaptureService = null;
        }

        private void OnKeyboardCaptureServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(IKeyboardCaptureService.IsRecordingKeyboardKey))
                return;

            if (_keyboardCaptureService is null || !_keyboardCaptureService.IsRecordingKeyboardKey)
                return;

            // Move keyboard focus onto this control so we reliably receive the next key press.
            _view.Dispatcher.BeginInvoke(new Action(() =>
            {
                _view.Focus();
                Keyboard.Focus(_view);
            }), DispatcherPriority.Input);
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_viewModel is null)
                return;

            if (_viewModel.KeyboardCaptureService.IsRecordingKeyboardKey && e.Key == Key.Escape)
            {
                _viewModel.CancelKeyboardKeyRecording();
                e.Handled = true;
                return;
            }

            if (_viewModel.TryCaptureKeyboardKey(e.Key, e.SystemKey))
                e.Handled = true;
        }
    }
}
