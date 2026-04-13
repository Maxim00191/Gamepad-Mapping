using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Windows.Media;
using Gamepad_Mapping.Utils.ControllerSvg;
using Gamepad_Mapping.ViewModels;

namespace Gamepad_Mapping.Views.Sections;

public partial class ControllerVisualView : UserControl
{
    public ControllerVisualView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (ControllerSvgDrawingImageLoader.TryLoadAligned("Xbox.svg", out var alignedImage, out var viewport, out var layerTransform))
        {
            ControllerSvgImage.Source = alignedImage;
            ControllerVisualSvgRoot.Width = viewport.Width;
            ControllerVisualSvgRoot.Height = viewport.Height;
            InteractionLayer.RenderTransform = layerTransform;
            InteractionLayer.RenderTransformOrigin = new Point(0, 0);
        }
        else if (ControllerSvgDrawingImageLoader.TryLoad("Xbox.svg") is DrawingImage drawingImage)
        {
            ControllerSvgImage.Source = drawingImage;
        }

        var interactivePath = Resources["InteractivePathStyle"] as Style;
        var interactiveRect = Resources["InteractiveRectangleStyle"] as Style;
        if (interactivePath is null || interactiveRect is null) return;

        XboxControllerInteractiveLayerBuilder.Populate(
            InteractionLayer,
            interactivePath,
            interactiveRect,
            Path_MouseLeftButtonDown,
            Path_MouseEnter,
            Path_MouseLeave);

        if (DataContext is ControllerVisualViewModel vm)
            UpdateSelection(vm.SelectedElementName);
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ControllerVisualViewModel oldVm)
        {
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;
        }
        if (e.NewValue is ControllerVisualViewModel newVm)
        {
            newVm.PropertyChanged += OnViewModelPropertyChanged;
            UpdateSelection(newVm.SelectedElementName);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ControllerVisualViewModel.SelectedElementName) && sender is ControllerVisualViewModel vm)
        {
            UpdateSelection(vm.SelectedElementName);
        }
    }

    private void UpdateSelection(string? selectedElementId)
    {
        var selectedStyle = Resources["SelectedPathStyle"] as Style;
        var interactiveStyle = Resources["InteractivePathStyle"] as Style;

        if (interactiveStyle == null) return;

        foreach (var child in FindVisualChildren<FrameworkElement>(this))
        {
            if (child.Tag is not string tag) continue;

            switch (child)
            {
                case Path path:
                    path.Style = tag == selectedElementId
                        ? selectedStyle ?? interactiveStyle
                        : interactiveStyle;
                    break;
                case Rectangle rect:
                    var rectInteractive = Resources["InteractiveRectangleStyle"] as Style;
                    var rectSelected = Resources["SelectedRectangleStyle"] as Style;
                    if (rectInteractive is null) break;
                    rect.Style = tag == selectedElementId
                        ? rectSelected ?? rectInteractive
                        : rectInteractive;
                    break;
            }
        }
    }

    private static System.Collections.Generic.IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
    {
        if (depObj == null) yield break;
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(depObj); i++)
        {
            DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(depObj, i);
            if (child != null && child is T t)
            {
                yield return t;
            }

            if (child is null) continue;
            foreach (T childOfChild in FindVisualChildren<T>(child))
            {
                yield return childOfChild;
            }
        }
    }

    private void Path_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && DataContext is ControllerVisualViewModel vm)
        {
            vm.SelectElementCommand.Execute(element.Tag as string);
        }
    }

    private void Path_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is FrameworkElement element && DataContext is ControllerVisualViewModel vm)
        {
            vm.HoverElementCommand.Execute(element.Tag as string);
        }
    }

    private void Path_MouseLeave(object sender, MouseEventArgs e)
    {
        if (DataContext is ControllerVisualViewModel vm)
        {
            vm.HoverElementCommand.Execute(null);
        }
    }
}
