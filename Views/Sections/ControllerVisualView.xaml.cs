using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Xml.Linq;
using Gamepad_Mapping.Interfaces.Services;
using Gamepad_Mapping.Models.Core.Visual;
using Gamepad_Mapping.Utils.ControllerSvg;
using Gamepad_Mapping.ViewModels;
using GamepadMapperGUI.Models.ControllerVisual;
using GamepadMapperGUI.Utils;

namespace Gamepad_Mapping.Views.Sections;

public partial class ControllerVisualView : UserControl
{
    private Point _lastMousePosition;
    private bool _isPanning;

    public ControllerVisualView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;

        MainRoot.MouseWheel += OnMouseWheel;
        MainRoot.MouseDown += OnMouseDown;
        MainRoot.MouseMove += OnMouseMove;
        MainRoot.MouseUp += OnMouseUp;
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var pos = e.GetPosition(TransformContainer);
        var scaleFactor = e.Delta > 0 ? 1.1 : 1 / 1.1;

        var currentScale = MainScaleTransform.ScaleX;
        var newScale = currentScale * scaleFactor;

        const double minScale = 0.1;
        if (newScale < minScale)
        {
            newScale = minScale;
        }

        var actualScaleFactor = newScale / currentScale;

        MainScaleTransform.ScaleX = newScale;
        MainScaleTransform.ScaleY = newScale;

        MainTranslateTransform.X = (MainTranslateTransform.X - pos.X) * actualScaleFactor + pos.X;
        MainTranslateTransform.Y = (MainTranslateTransform.Y - pos.Y) * actualScaleFactor + pos.Y;

        e.Handled = true;
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle || e.ChangedButton == MouseButton.Right)
        {
            _lastMousePosition = e.GetPosition(MainRoot);
            _isPanning = true;
            MainRoot.CaptureMouse();
            e.Handled = true;
        }
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_isPanning)
        {
            var pos = e.GetPosition(MainRoot);
            var delta = pos - _lastMousePosition;
            _lastMousePosition = pos;

            MainTranslateTransform.X += delta.X;
            MainTranslateTransform.Y += delta.Y;

            e.Handled = true;
        }
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isPanning && (e.ChangedButton == MouseButton.Middle || e.ChangedButton == MouseButton.Right))
        {
            _isPanning = false;
            MainRoot.ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyControllerVisualLayout();
        ResetView();
    }

    private void ResetView()
    {
        if (MainRoot.ActualWidth == 0 || MainRoot.ActualHeight == 0) return;

        double scale = Math.Min(MainRoot.ActualWidth / ControllerVisualSvgRoot.Width, 
                               MainRoot.ActualHeight / ControllerVisualSvgRoot.Height) * 0.8;
        
        MainScaleTransform.ScaleX = scale;
        MainScaleTransform.ScaleY = scale;
        
        MainTranslateTransform.X = (MainRoot.ActualWidth - ControllerVisualSvgRoot.Width * scale) / 2;
        MainTranslateTransform.Y = (MainRoot.ActualHeight - ControllerVisualSvgRoot.Height * scale) / 2;
    }

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
            string AccessibleName(string logicalId) =>
                DataContext is ControllerVisualViewModel vm
                    ? vm.GetElementInteractionLabel(logicalId)
                    : logicalId;

            ControllerVisualInteractiveLayerBuilder.Populate(
                InteractionLayer,
                svgRoot,
                layout,
                interactivePath,
                interactiveRect,
                Path_MouseLeftButtonDown,
                Path_MouseEnter,
                Path_MouseLeave,
                AccessibleName);

            var fallbackAnchors = ControllerVisualOverlayLayerBuilder.ComputeOverlayAnchors(svgRoot, layout);
            var viewportSize = new Size(InteractionLayer.Width, InteractionLayer.Height);
            if (DataContext is ControllerVisualViewModel overlayVm)
            {
                overlayVm.ApplyOverlayAnchorPositions(fallbackAnchors, viewportSize);
                Dispatcher.BeginInvoke(
                    new Action(() => ApplyVisualOverlayAnchors(fallbackAnchors, viewportSize)),
                    DispatcherPriority.Render);
            }
        }
        else
        {
            InteractionLayer.Children.Clear();
        }
    }

    private void ApplyVisualOverlayAnchors(
        IReadOnlyDictionary<string, Point> fallbackAnchors,
        Size viewportSize)
    {
        if (DataContext is not ControllerVisualViewModel vm) return;
        if (InteractionLayer.Children.Count == 0) return;

        var visual = ControllerVisualAnchorPositions.FromInteractionLayer(InteractionLayer);
        if (visual.Count == 0) return;

        var merged = new Dictionary<string, Point>(visual, StringComparer.OrdinalIgnoreCase);
        foreach (var kv in fallbackAnchors)
            merged.TryAdd(kv.Key, kv.Value);

        vm.ApplyOverlayAnchorPositions(merged, viewportSize);
    }

    private void ApplyControllerVisualLayout()
    {
        if (DataContext is not ControllerVisualViewModel vm) return;

        var layout = vm.ActiveLayout;

        if (vm.Loader.TryLoad(layout, out var aligned))
        {
            ControllerSvgImage.Source = aligned.Image;
            ApplyAlignedDiagramSurface(aligned.Viewport.Width, aligned.Viewport.Height, aligned.InteractionLayerTransform);
            PopulateInteractionLayer(aligned.SvgRoot, layout);
        }
        else
        {
            ControllerSvgImage.Source = ControllerSvgDrawingImageLoader.TryLoad(layout.SvgFileName);
            ApplyAlignedDiagramSurface(300, 250, null);
            PopulateInteractionLayer(svgRoot: null, layout);
        }

        UpdateSelection(vm.SelectedElementName);
    }

    private void ApplyAlignedDiagramSurface(double width, double height, Transform? interactionTransform)
    {
        ControllerVisualSvgRoot.Width = width;
        ControllerVisualSvgRoot.Height = height;
        InteractionLayer.Width = width;
        InteractionLayer.Height = height;
        OverlayLayer.Width = width;
        OverlayLayer.Height = height;

        InteractionLayer.RenderTransform = interactionTransform;
        InteractionLayer.RenderTransformOrigin = new Point(0, 0);
        OverlayLayer.RenderTransform = interactionTransform;
        OverlayLayer.RenderTransformOrigin = new Point(0, 0);
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ControllerVisualViewModel oldVm)
        {
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;
            if (oldVm.HighlightService != null)
                oldVm.HighlightService.PropertyChanged -= OnSceneChanged;
        }

        if (e.NewValue is ControllerVisualViewModel newVm)
        {
            newVm.PropertyChanged += OnViewModelPropertyChanged;
            if (newVm.HighlightService != null)
                newVm.HighlightService.PropertyChanged += OnSceneChanged;
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
        else if (e.PropertyName == nameof(ControllerVisualViewModel.HighlightService))
        {
            UpdateSelection(vm.SelectedElementName);
        }
    }

    private void OnSceneChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IControllerVisualHighlightService.CurrentScene))
        {
            if (DataContext is ControllerVisualViewModel vm)
            {
                Dispatcher.BeginInvoke(
                    new Action(() => UpdateSelection(vm.SelectedElementName)),
                    DispatcherPriority.Render);
            }
        }
    }

    private void UpdateSelection(string? selectedElementId)
    {
        var selectedStyle = Resources["SelectedPathStyle"] as Style;
        var chordStyle = Resources["ChordPartPathStyle"] as Style;
        var interactiveStyle = Resources["InteractivePathStyle"] as Style;

        if (interactiveStyle is null) return;

        var rectInteractive = Resources["InteractiveRectangleStyle"] as Style;
        var rectSelected = Resources["SelectedRectangleStyle"] as Style;

        var vm = DataContext as ControllerVisualViewModel;
        var scene = vm?.HighlightService?.CurrentScene;

        foreach (var child in InteractionLayer.Children)
        {
            if (child is not FrameworkElement fe)
                continue;
            if (fe.Tag is not string tag)
                continue;

            var elementState = scene?.Elements.FirstOrDefault(e => e.ElementId == tag);
            var isChordPart = elementState?.Highlight == ControllerVisualHighlightKind.ChordSecondary;

            switch (fe)
            {
                case Path path:
                    if (tag == selectedElementId)
                        path.Style = selectedStyle ?? interactiveStyle;
                    else if (isChordPart)
                        path.Style = chordStyle ?? interactiveStyle;
                    else
                        path.Style = interactiveStyle;
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

            for (var cur = element as DependencyObject; cur != null; cur = VisualTreeHelper.GetParent(cur))
            {
                if (cur is VisualEditorView ve && ve.DataContext is VisualEditorViewModel visualVm)
                {
                    if (visualVm.ShowVisualCreateMappingCallout)
                    {
                        visualVm.CreateMappingCommand.Execute(null);
                    }
                    break;
                }
            }
        }
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
