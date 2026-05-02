#nullable enable

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Gamepad_Mapping.ViewModels;
using GamepadMapperGUI.Services.Infrastructure;

namespace Gamepad_Mapping.Views.Automation;

public partial class AutomationRoiPreviewWindow : Window
{
    public AutomationRoiPreviewWindow()
    {
        InitializeComponent();
        Title = AppUiLocalization.GetString("AutomationRoiPreview_WindowTitle");
    }

    private void ImageScroll_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (DataContext is AutomationRoiPreviewViewModel vm)
            vm.NotifyViewportSize(e.NewSize.Width, e.NewSize.Height);
    }

    private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control)
            return;

        if (DataContext is AutomationRoiPreviewViewModel vm)
        {
            vm.AdjustZoomFromWheel(e.Delta);
            e.Handled = true;
        }
    }

    private void PreviewImageArea_MouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not FrameworkElement fe || DataContext is not AutomationRoiPreviewViewModel vm)
            return;

        var p = e.GetPosition(fe);
        vm.UpdateCursorSample(p.X, p.Y);
    }

    private void PreviewImageArea_MouseLeave(object sender, MouseEventArgs e)
    {
        if (DataContext is AutomationRoiPreviewViewModel vm)
            vm.ClearCursorSample();
    }
}
