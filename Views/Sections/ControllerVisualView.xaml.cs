using System.ComponentModel;
using System.Xml.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using Gamepad_Mapping.Utils.ControllerSvg;
using Gamepad_Mapping.ViewModels;
using GamepadMapperGUI.Models.ControllerVisual;
using GamepadMapperGUI.Utils;

namespace Gamepad_Mapping.Views.Sections;

public partial class ControllerVisualView : UserControl
{
    public ControllerVisualView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => ApplyControllerVisualLayout();

    private void PopulateInteractionLayer(XElement? svgRoot, ControllerVisualLayoutDescriptor layout)
    {
        var interactivePath = Resources["InteractivePathStyle"] as Style;
        var interactiveRect = Resources["InteractiveRectangleStyle"] as Style;
        if (interactivePath is null || interactiveRect is null) return;

        if (svgRoot is null)
        {
            var path = AppPaths.GetControllerSvgPath(layout.SvgFileName);
            ControllerSvgViewport.TryReadSvgRoot(path, out svgRoot, out _);
        }

        if (svgRoot is not null)
        {
            ControllerVisualInteractiveLayerBuilder.Populate(
                InteractionLayer,
                svgRoot,
                layout,
                interactivePath,
                interactiveRect,
                Path_MouseLeftButtonDown,
                Path_MouseEnter,
                Path_MouseLeave);
        }
        else
            InteractionLayer.Children.Clear();
    }

    private void ApplyControllerVisualLayout()
    {
        if (DataContext is not ControllerVisualViewModel vm) return;

        var layout = vm.ActiveLayout;

        if (vm.Loader.TryLoad(layout, out var aligned))
        {
            ControllerSvgImage.Source = aligned.Image;
            ControllerVisualSvgRoot.Width = aligned.Viewport.Width;
            ControllerVisualSvgRoot.Height = aligned.Viewport.Height;
            InteractionLayer.RenderTransform = aligned.InteractionLayerTransform;
            InteractionLayer.RenderTransformOrigin = new Point(0, 0);
            PopulateInteractionLayer(aligned.SvgRoot, layout);
        }
        else
        {
            ControllerSvgImage.Source = ControllerSvgDrawingImageLoader.TryLoad(layout.SvgFileName);
            PopulateInteractionLayer(svgRoot: null, layout);
        }

        UpdateSelection(vm.SelectedElementName);
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ControllerVisualViewModel oldVm)
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;

        if (e.NewValue is ControllerVisualViewModel newVm)
        {
            newVm.PropertyChanged += OnViewModelPropertyChanged;
            UpdateSelection(newVm.SelectedElementName);
            ApplyControllerVisualLayout();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not ControllerVisualViewModel vm) return;

        if (e.PropertyName == nameof(ControllerVisualViewModel.SelectedElementName))
            UpdateSelection(vm.SelectedElementName);
        else if (e.PropertyName == nameof(ControllerVisualViewModel.ActiveLayout))
            ApplyControllerVisualLayout();
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
            vm.SelectElementCommand.Execute(element.Tag as string);
    }

    private void Path_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is FrameworkElement element && DataContext is ControllerVisualViewModel vm)
            vm.HoverElementCommand.Execute(element.Tag as string);
    }

    private void Path_MouseLeave(object sender, MouseEventArgs e)
    {
        if (DataContext is ControllerVisualViewModel vm)
            vm.HoverElementCommand.Execute(null);
    }
}
