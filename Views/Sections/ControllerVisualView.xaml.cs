using System.ComponentModel;
using System.Xml.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
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
        var fileName = ControllerSvgConstants.XboxControllerSvgFileName;

        if (ControllerSvgDrawingImageLoader.TryLoadAligned(
                fileName,
                out var alignedImage,
                out var viewport,
                out var layerTransform,
                out var svgRoot))
        {
            ControllerSvgImage.Source = alignedImage;
            ControllerVisualSvgRoot.Width = viewport.Width;
            ControllerVisualSvgRoot.Height = viewport.Height;
            InteractionLayer.RenderTransform = layerTransform;
            InteractionLayer.RenderTransformOrigin = new Point(0, 0);
            PopulateInteractionLayer(svgRoot);
        }
        else
        {
            if (ControllerSvgDrawingImageLoader.TryLoad(fileName) is { } drawingImage)
                ControllerSvgImage.Source = drawingImage;
            PopulateInteractionLayer(svgRoot: null);
        }

        if (DataContext is ControllerVisualViewModel vm)
            UpdateSelection(vm.SelectedElementName);
    }

    private void PopulateInteractionLayer(XElement? svgRoot)
    {
        var interactivePath = Resources["InteractivePathStyle"] as Style;
        var interactiveRect = Resources["InteractiveRectangleStyle"] as Style;
        if (interactivePath is null || interactiveRect is null) return;

        if (svgRoot is not null)
        {
            XboxControllerInteractiveLayerBuilder.Populate(
                InteractionLayer,
                svgRoot,
                interactivePath,
                interactiveRect,
                Path_MouseLeftButtonDown,
                Path_MouseEnter,
                Path_MouseLeave);
        }
        else
        {
            XboxControllerInteractiveLayerBuilder.Populate(
                InteractionLayer,
                interactivePath,
                interactiveRect,
                Path_MouseLeftButtonDown,
                Path_MouseEnter,
                Path_MouseLeave);
        }
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

        if (interactiveStyle is null) return;

        var rectInteractive = Resources["InteractiveRectangleStyle"] as Style;
        var rectSelected = Resources["SelectedRectangleStyle"] as Style;

        foreach (var child in InteractionLayer.Children)
        {
            if (child is not FrameworkElement fe)
                continue;
            if (fe.Tag is not string tag)
                continue;

            switch (fe)
            {
                case Path path:
                    path.Style = tag == selectedElementId
                        ? selectedStyle ?? interactiveStyle
                        : interactiveStyle;
                    break;
                case Rectangle rect:
                    if (rectInteractive is null) break;
                    rect.Style = tag == selectedElementId
                        ? rectSelected ?? rectInteractive
                        : rectInteractive;
                    break;
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
