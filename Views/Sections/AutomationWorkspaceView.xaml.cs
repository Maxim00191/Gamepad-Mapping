#nullable enable

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Gamepad_Mapping.ViewModels;
using GamepadMapperGUI.Models.Automation;
using GamepadMapperGUI.Services.Infrastructure;

namespace Gamepad_Mapping.Views.Sections;

public partial class AutomationWorkspaceView
{
    public AutomationWorkspaceView()
    {
        InitializeComponent();
        Loaded += AutomationWorkspaceView_Loaded;
    }

    private AutomationWorkspaceViewModel? WorkspaceVm => DataContext as AutomationWorkspaceViewModel;
    private bool _isConnectionDragActive;
    private bool _isMiddlePanning;
    private Point _middlePanStartPoint;
    private double _panStartHorizontalOffset;
    private double _panStartVerticalOffset;
    private bool _isLeftPressActive;
    private bool _isSelectionMarqueeActive;
    private bool _isNodeDragActive;
    private AutomationCanvasNodeViewModel? _draggingNode;
    private Point _nodeDragLastCanvasPoint;
    private Point _selectionStartCanvasPoint;
    private Point _selectionStartScreenPoint;
    private bool _isMiniMapDragging;
    private const double SelectionDragStartTolerance = 6d;

    private void PaletteRow_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _paletteDragOrigin = e.GetPosition(null);
        _paletteDragSource = sender as FrameworkElement;
    }

    private Point _paletteDragOrigin;
    private FrameworkElement? _paletteDragSource;

    private void PaletteRow_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || WorkspaceVm is null || _paletteDragSource is null)
            return;

        var pos = e.GetPosition(null);
        if ((pos - _paletteDragOrigin).Length < 6)
            return;

        if (_paletteDragSource.DataContext is not AutomationNodePaletteItemViewModel item ||
            string.IsNullOrEmpty(item.NodeTypeId))
            return;

        var data = new DataObject("AutomationNodeTypeId", item.NodeTypeId);
        DragDrop.DoDragDrop(_paletteDragSource, data, DragDropEffects.Copy);
        e.Handled = true;
    }

    private void PaletteRow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount < 2 || WorkspaceVm is null)
            return;

        if (sender is not FrameworkElement fe ||
            fe.DataContext is not AutomationNodePaletteItemViewModel item ||
            string.IsNullOrWhiteSpace(item.NodeTypeId))
            return;

        var centerX = CanvasScrollViewer.HorizontalOffset + (CanvasScrollViewer.ViewportWidth / 2d);
        var centerY = CanvasScrollViewer.VerticalOffset + (CanvasScrollViewer.ViewportHeight / 2d);
        WorkspaceVm.AddNodeAtViewportCenter(item.NodeTypeId, centerX, centerY);
        e.Handled = true;
    }

    private void CanvasSurface_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent("AutomationNodeTypeId"))
            e.Effects = DragDropEffects.Copy;
        else
            e.Effects = DragDropEffects.None;

        e.Handled = true;
    }

    private void CanvasSurface_Drop(object sender, DragEventArgs e)
    {
        if (WorkspaceVm is null)
            return;

        if (!e.Data.GetDataPresent("AutomationNodeTypeId"))
            return;

        var id = e.Data.GetData("AutomationNodeTypeId") as string;
        if (string.IsNullOrEmpty(id))
            return;

        var p = ToLogicalCanvasPoint(e.GetPosition(CanvasSurface));
        WorkspaceVm.AddNodeAt(id, p.X, p.Y);
        e.Handled = true;
    }

    private void NodeChrome_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (WorkspaceVm is null)
            return;

        var originalSource = e.OriginalSource as DependencyObject;
        if (IsInteractiveChildForNodeDrag(originalSource))
        {
            if (sender is FrameworkElement interactiveFe &&
                interactiveFe.DataContext is AutomationCanvasNodeViewModel interactiveNode)
            {
                WorkspaceVm.SelectNodeForPointerDown(
                    interactiveNode,
                    toggleSelection: Keyboard.Modifiers.HasFlag(ModifierKeys.Control));
            }

            return;
        }

        if (sender is FrameworkElement fe && fe.DataContext is AutomationCanvasNodeViewModel node)
            e.Handled = TryHandleNodePointerDown(node, originalSource, e.GetPosition(CanvasSurface));
    }

    private void NodeChrome_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isNodeDragActive)
            return;

        TryUpdateNodeDrag(e);
    }

    private void NodeChrome_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (WorkspaceVm is null ||
            sender is not FrameworkElement fe ||
            fe.DataContext is not AutomationCanvasNodeViewModel node)
        {
            return;
        }

        WorkspaceVm.SelectNodeForPointerDown(node, toggleSelection: false);
        OpenNodeContextMenu(fe, node);
        e.Handled = true;
    }

    private void NodeChrome_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isNodeDragActive)
            return;

        EndNodeDrag();
        e.Handled = true;
    }

    private void NodeChrome_MouseEnter(object sender, MouseEventArgs e)
    {
        if (WorkspaceVm is null || sender is not FrameworkElement fe || fe.DataContext is not AutomationCanvasNodeViewModel node)
            return;

        WorkspaceVm.SetNodeHover(node.Id, true);
    }

    private void NodeChrome_MouseLeave(object sender, MouseEventArgs e)
    {
        if (WorkspaceVm is null || sender is not FrameworkElement fe || fe.DataContext is not AutomationCanvasNodeViewModel node)
            return;

        WorkspaceVm.SetNodeHover(node.Id, false);
    }

    private void NodeThumb_DragStarted(object sender, DragStartedEventArgs e)
    {
        if (WorkspaceVm is null || sender is not Thumb t || t.DataContext is not AutomationCanvasNodeViewModel node)
            return;

        WorkspaceVm.BeginNodeMoveSession(node);
    }

    private void NodeThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (WorkspaceVm is null || sender is not Thumb t || t.DataContext is not AutomationCanvasNodeViewModel node)
            return;

        var alt = Keyboard.Modifiers.HasFlag(ModifierKeys.Alt);
        var delta = ToLogicalCanvasDelta(e.HorizontalChange, e.VerticalChange);
        WorkspaceVm.DragNode(node, delta.X, delta.Y, alt);
    }

    private void NodeThumb_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (WorkspaceVm is null || sender is not Thumb t || t.DataContext is not AutomationCanvasNodeViewModel node)
            return;

        WorkspaceVm.EndNodeMoveSession(node);
    }

    private void PortHandle_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (WorkspaceVm is null || sender is not FrameworkElement fe)
            return;
        if (fe.DataContext is not AutomationNodePortViewModel port)
            return;

        var node = FindAncestorDataContext<AutomationCanvasNodeViewModel>(fe);
        if (node is null)
            return;

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            WorkspaceVm.DisconnectPortConnections(node.Id, port.Id, port.IsOutput);
            e.Handled = true;
            return;
        }

        WorkspaceVm.BeginConnectionDrag(node.Id, port.Id, port.IsOutput);
        _isConnectionDragActive = true;
        Mouse.Capture(this, CaptureMode.SubTree);
        PreviewMouseMove += AutomationWorkspaceView_PreviewMouseMove;
        PreviewMouseLeftButtonUp += AutomationWorkspaceView_PreviewMouseLeftButtonUp;
        e.Handled = true;
    }

    private void PortHandle_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe)
            PublishPortAnchor(fe);
    }

    private void PortHandle_LayoutUpdated(object? sender, EventArgs e)
    {
        if (sender is FrameworkElement fe)
            PublishPortAnchor(fe);
    }

    private void PortHandle_Unloaded(object sender, RoutedEventArgs e)
    {
        if (WorkspaceVm is null || sender is not FrameworkElement fe || fe.DataContext is not AutomationNodePortViewModel port)
            return;
        var node = FindAncestorDataContext<AutomationCanvasNodeViewModel>(fe);
        if (node is null)
            return;

        WorkspaceVm.ClearPortAnchor(node.Id, port.Id, port.IsOutput);
    }

    private void PortHandle_MouseEnter(object sender, MouseEventArgs e)
    {
        if (WorkspaceVm is null || sender is not FrameworkElement fe || fe.DataContext is not AutomationNodePortViewModel port)
            return;
        var node = FindAncestorDataContext<AutomationCanvasNodeViewModel>(fe);
        if (node is null)
            return;

        WorkspaceVm.SetPortHover(node.Id, port.Id, port.IsOutput, true);
    }

    private void PortHandle_MouseLeave(object sender, MouseEventArgs e)
    {
        if (WorkspaceVm is null || sender is not FrameworkElement fe || fe.DataContext is not AutomationNodePortViewModel port)
            return;
        var node = FindAncestorDataContext<AutomationCanvasNodeViewModel>(fe);
        if (node is null)
            return;

        WorkspaceVm.SetPortHover(node.Id, port.Id, port.IsOutput, false);
    }

    private void AutomationWorkspaceView_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (WorkspaceVm is null || !_isConnectionDragActive)
            return;

        var point = ToLogicalCanvasPoint(e.GetPosition(CanvasSurface));
        var hoveredPort = TryFindPortAtPoint(point);
        if (hoveredPort is not null)
        {
            WorkspaceVm.UpdateConnectionDrag(
                point.X,
                point.Y,
                hoveredPort.Value.Node.Id,
                hoveredPort.Value.Port.Id,
                hoveredPort.Value.Port.IsOutput);
            return;
        }

        WorkspaceVm.UpdateConnectionDrag(point.X, point.Y, null, null, null);
    }

    private void AutomationWorkspaceView_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isConnectionDragActive || WorkspaceVm is null)
            return;

        var point = ToLogicalCanvasPoint(e.GetPosition(CanvasSurface));
        var hoveredPort = TryFindPortAtPoint(point);
        if (hoveredPort is { } hovered)
        {
            WorkspaceVm.UpdateConnectionDrag(
                point.X,
                point.Y,
                hovered.Node.Id,
                hovered.Port.Id,
                hovered.Port.IsOutput);
        }

        if (hoveredPort is null)
            WorkspaceVm.UpdateConnectionDrag(point.X, point.Y, null, null, null);

        WorkspaceVm.CompleteConnectionDrag();
        EndConnectionDrag();
    }

    private void EndConnectionDrag()
    {
        if (!_isConnectionDragActive || WorkspaceVm is null)
            return;

        _isConnectionDragActive = false;
        WorkspaceVm.CancelConnectionDrag();
        if (!_isMiddlePanning && !_isSelectionMarqueeActive && Mouse.Captured == this)
            Mouse.Capture(null);
        PreviewMouseMove -= AutomationWorkspaceView_PreviewMouseMove;
        PreviewMouseLeftButtonUp -= AutomationWorkspaceView_PreviewMouseLeftButtonUp;
    }

    private (AutomationCanvasNodeViewModel Node, AutomationNodePortViewModel Port)? TryFindPortAtPoint(Point canvasPoint)
    {
        var hitPoint = ToScaledCanvasPoint(canvasPoint);
        if (CanvasSurface.InputHitTest(hitPoint) is not DependencyObject current)
            return null;

        FrameworkElement? portElement = null;
        while (current is not null)
        {
            if (current is FrameworkElement fe && fe.DataContext is AutomationNodePortViewModel)
            {
                portElement = fe;
                break;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        if (portElement?.DataContext is not AutomationNodePortViewModel port)
            return null;
        var node = FindAncestorDataContext<AutomationCanvasNodeViewModel>(portElement);
        return node is null ? null : (node, port);
    }

    private static T? FindAncestorDataContext<T>(DependencyObject? start) where T : class =>
        TryFindAncestorDataContext<T>(start);

    private void InlineNodeEditor_LostFocus_Commit(object sender, RoutedEventArgs e)
    {
        if (WorkspaceVm is null)
            return;
        if (sender is not FrameworkElement fe || fe.DataContext is not AutomationInlineNodeFieldViewModel field)
            return;

        WorkspaceVm.CommitInlineNodeFieldCommand.Execute(field);
    }

    private void InlineNodeEditor_KeyDown_Commit(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || WorkspaceVm is null)
            return;
        if (sender is not FrameworkElement fe || fe.DataContext is not AutomationInlineNodeFieldViewModel field)
            return;
        if (field.IsMultilineTextField && !Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            return;

        WorkspaceVm.CommitInlineNodeFieldCommand.Execute(field);
        e.Handled = true;
    }

    private void InlineNodeEditor_LoopScopeChoice_DropDownOpened(object sender, EventArgs e)
    {
        if (WorkspaceVm is null)
            return;
        if (sender is not FrameworkElement fe || fe.DataContext is not AutomationInlineNodeFieldViewModel field)
            return;

        WorkspaceVm.RefreshLoopScopeChoiceItems(field);
    }

    private void InlineNodeEditor_Choice_DropDownClosed(object sender, EventArgs e)
    {
        if (WorkspaceVm is null)
            return;
        if (sender is not FrameworkElement fe || fe.DataContext is not AutomationInlineNodeFieldViewModel field)
            return;

        WorkspaceVm.CommitInlineNodeFieldCommand.Execute(field);
    }

    private void CanvasScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (WorkspaceVm is null)
            return;

        e.Handled = true;
        var oldZoom = WorkspaceVm.Zoom;
        var step = e.Delta > 0 ? 0.1 : -0.1;
        var nextZoom = Math.Clamp(oldZoom + step, 0.35, 2.0);
        if (Math.Abs(nextZoom - oldZoom) < 0.001)
            return;

        var viewportPoint = e.GetPosition(CanvasScrollViewer);
        var absoluteX = CanvasScrollViewer.HorizontalOffset + viewportPoint.X;
        var absoluteY = CanvasScrollViewer.VerticalOffset + viewportPoint.Y;
        var factor = nextZoom / oldZoom;
        WorkspaceVm.Zoom = nextZoom;
        CanvasScrollViewer.ScrollToHorizontalOffset((absoluteX * factor) - viewportPoint.X);
        CanvasScrollViewer.ScrollToVerticalOffset((absoluteY * factor) - viewportPoint.Y);
    }

    private void EdgeLine_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (WorkspaceVm is null || sender is not FrameworkElement fe)
            return;
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            return;
        if (fe.DataContext is not AutomationEdgeDisplayViewModel edge)
            return;

        if (WorkspaceVm.DisconnectEdge(edge.EdgeId))
            e.Handled = true;
    }

    private void EdgePath_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is AutomationEdgeDisplayViewModel edge)
            edge.IsHovered = true;
    }

    private void EdgePath_MouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is AutomationEdgeDisplayViewModel edge)
            edge.IsHovered = false;
    }

    private void CanvasSurface_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle)
            return;

        BeginMiddlePanning(e.GetPosition(this));
        e.Handled = true;
    }

    private void CanvasSurface_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!TryHandleMiddlePanningMouseUp(e))
            return;

        e.Handled = true;
    }

    private void AutomationRoot_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!TryHandleMiddlePanningMouseUp(e))
            return;

        e.Handled = true;
    }

    private void CanvasSurface_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_isNodeDragActive && Mouse.LeftButton != MouseButtonState.Pressed)
            EndNodeDrag();
        else if (_isNodeDragActive)
            TryUpdateNodeDrag(e);

        if (_isMiddlePanning && e.MiddleButton != MouseButtonState.Pressed)
        {
            EndMiddlePanning();
            e.Handled = true;
            return;
        }

        if (_isLeftPressActive && !_isSelectionMarqueeActive)
        {
            var current = e.GetPosition(this);
            if (Mouse.LeftButton == MouseButtonState.Pressed &&
                (current - _selectionStartScreenPoint).Length > SelectionDragStartTolerance)
            {
                BeginSelectionRectangle();
            }
        }

        if (_isMiddlePanning)
        {
            var current = e.GetPosition(this);
            var delta = current - _middlePanStartPoint;
            CanvasScrollViewer.ScrollToHorizontalOffset(_panStartHorizontalOffset - delta.X);
            CanvasScrollViewer.ScrollToVerticalOffset(_panStartVerticalOffset - delta.Y);
            e.Handled = true;
            return;
        }

        if (_isSelectionMarqueeActive)
        {
            UpdateSelectionRectangle(ToLogicalCanvasPoint(e.GetPosition(CanvasSurface)));
            e.Handled = true;
        }
    }

    private void CanvasSurface_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (WorkspaceVm is null)
            return;
        var originalSource = e.OriginalSource as DependencyObject;
        if (IsInteractiveChildForNodeDrag(originalSource))
            return;

        var node = TryFindAncestorDataContext<AutomationCanvasNodeViewModel>(originalSource);
        if (node is not null)
        {
            e.Handled = TryHandleNodePointerDown(node, originalSource, e.GetPosition(CanvasSurface));
            return;
        }

        AutomationRoot.Focus();
        WorkspaceVm.SelectSingleNode(null);
        _isLeftPressActive = true;
        _selectionStartCanvasPoint = ToLogicalCanvasPoint(e.GetPosition(CanvasSurface));
        _selectionStartScreenPoint = e.GetPosition(this);
    }

    private void CanvasSurface_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isLeftPressActive = false;

        if (!_isSelectionMarqueeActive)
            return;

        CommitSelectionRectangle(ToLogicalCanvasPoint(e.GetPosition(CanvasSurface)));
        EndSelectionRectangle();
        e.Handled = true;
    }

    private void UpdateSelectionRectangle(Point currentCanvasPoint)
    {
        var left = Math.Min(_selectionStartCanvasPoint.X, currentCanvasPoint.X);
        var top = Math.Min(_selectionStartCanvasPoint.Y, currentCanvasPoint.Y);
        var width = Math.Abs(currentCanvasPoint.X - _selectionStartCanvasPoint.X);
        var height = Math.Abs(currentCanvasPoint.Y - _selectionStartCanvasPoint.Y);
        Canvas.SetLeft(SelectionRectOverlay, left);
        Canvas.SetTop(SelectionRectOverlay, top);
        SelectionRectOverlay.Width = width;
        SelectionRectOverlay.Height = height;
    }

    private void CommitSelectionRectangle(Point currentCanvasPoint)
    {
        if (WorkspaceVm is null)
            return;

        var additive = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        WorkspaceVm.SelectNodesInRectangle(
            _selectionStartCanvasPoint.X,
            _selectionStartCanvasPoint.Y,
            currentCanvasPoint.X,
            currentCanvasPoint.Y,
            additive);
    }

    private void EndSelectionRectangle()
    {
        _isSelectionMarqueeActive = false;
        SelectionRectOverlay.Visibility = Visibility.Collapsed;
        SelectionRectOverlay.Width = 0;
        SelectionRectOverlay.Height = 0;
        if (!_isConnectionDragActive && !_isMiddlePanning && Mouse.Captured == this)
            Mouse.Capture(null);
    }

    private void AutomationRoot_LostMouseCapture(object sender, MouseEventArgs e)
    {
        if (_isConnectionDragActive)
            EndConnectionDrag();
        if (_isMiddlePanning)
            EndMiddlePanning(releaseCaptureIfIdle: false);
        if (_isNodeDragActive)
            EndNodeDrag(releaseCaptureIfIdle: false);
    }

    private void AutomationRoot_Unloaded(object sender, RoutedEventArgs e)
    {
        EndConnectionDrag();
        EndMiddlePanning(releaseCaptureIfIdle: false);
        EndNodeDrag(releaseCaptureIfIdle: false);
        _isMiniMapDragging = false;
    }

    private void AutomationRoot_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (WorkspaceVm is not null &&
            e.Key == Key.Escape &&
            WorkspaceVm.IsAutomationRunInProgress &&
            WorkspaceVm.EmergencyStopCommand.CanExecute(null))
        {
            WorkspaceVm.EmergencyStopCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (WorkspaceVm is null || e.Key != Key.Delete)
        {
            if (e.Key == Key.Escape && _isConnectionDragActive)
            {
                EndConnectionDrag();
                e.Handled = true;
            }
            return;
        }
        if (!WorkspaceVm.DeleteSelectedCommand.CanExecute(null))
            return;

        WorkspaceVm.DeleteSelectedCommand.Execute(null);
        e.Handled = true;
    }

    private static T? TryFindAncestorDataContext<T>(DependencyObject? start) where T : class
    {
        var current = start;
        while (current is not null)
        {
            if (current is FrameworkElement fe && fe.DataContext is T typed)
                return typed;
            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private void BeginMiddlePanning(Point startPoint)
    {
        if (_isMiddlePanning)
            return;

        if (_isConnectionDragActive)
            EndConnectionDrag();
        if (_isNodeDragActive)
            EndNodeDrag();

        _isLeftPressActive = false;
        if (_isSelectionMarqueeActive)
            EndSelectionRectangle();
        _isMiddlePanning = true;
        _middlePanStartPoint = startPoint;
        _panStartHorizontalOffset = CanvasScrollViewer.HorizontalOffset;
        _panStartVerticalOffset = CanvasScrollViewer.VerticalOffset;
        Mouse.Capture(this, CaptureMode.SubTree);
        Mouse.OverrideCursor = Cursors.ScrollAll;
    }

    private bool TryHandleMiddlePanningMouseUp(MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle || !_isMiddlePanning)
            return false;

        EndMiddlePanning();
        return true;
    }

    private void EndMiddlePanning(bool releaseCaptureIfIdle = true)
    {
        _isMiddlePanning = false;
        Mouse.OverrideCursor = null;
        if (releaseCaptureIfIdle && !_isConnectionDragActive && !_isSelectionMarqueeActive && Mouse.Captured == this)
            Mouse.Capture(null);
    }

    private static Point ToLogicalCanvasPoint(Point canvasPoint) => canvasPoint;

    private static Point ToScaledCanvasPoint(Point logicalCanvasPoint) => logicalCanvasPoint;

    private Vector ToLogicalCanvasDelta(double dx, double dy)
    {
        var zoom = GetCurrentZoom();
        return new Vector(dx / zoom, dy / zoom);
    }

    private void PublishPortAnchor(FrameworkElement portElement)
    {
        if (WorkspaceVm is null || portElement.DataContext is not AutomationNodePortViewModel port)
            return;
        var node = FindAncestorDataContext<AutomationCanvasNodeViewModel>(portElement);
        if (node is null || CanvasSurface is null)
            return;
        if (portElement.ActualWidth <= 0 || portElement.ActualHeight <= 0)
            return;

        var centerPoint = new Point(portElement.ActualWidth * 0.5d, portElement.ActualHeight * 0.5d);
        var canvasPoint = ToLogicalCanvasPoint(portElement.TranslatePoint(centerPoint, CanvasSurface));
        WorkspaceVm.UpdatePortAnchor(node.Id, port.Id, port.IsOutput, canvasPoint.X, canvasPoint.Y);
    }

    private void EndNodeDrag(bool releaseCaptureIfIdle = true)
    {
        if (!_isNodeDragActive)
            return;

        if (WorkspaceVm is not null && _draggingNode is not null)
            WorkspaceVm.EndNodeMoveSession(_draggingNode);
        _draggingNode = null;
        _isNodeDragActive = false;
        PreviewMouseMove -= AutomationWorkspaceView_NodeDragPreviewMouseMove;
        PreviewMouseLeftButtonUp -= AutomationWorkspaceView_NodeDragPreviewMouseLeftButtonUp;

        if (releaseCaptureIfIdle &&
            !_isConnectionDragActive &&
            !_isMiddlePanning &&
            !_isSelectionMarqueeActive &&
            Mouse.Captured == this)
        {
            Mouse.Capture(null);
        }
    }

    private void StartNodeDrag(AutomationCanvasNodeViewModel node, Point startCanvasPoint)
    {
        if (WorkspaceVm is null || _isNodeDragActive)
            return;

        WorkspaceVm.BeginNodeMoveSession(node);
        _draggingNode = node;
        _nodeDragLastCanvasPoint = startCanvasPoint;
        _isNodeDragActive = true;
        Mouse.Capture(this, CaptureMode.SubTree);
        PreviewMouseMove += AutomationWorkspaceView_NodeDragPreviewMouseMove;
        PreviewMouseLeftButtonUp += AutomationWorkspaceView_NodeDragPreviewMouseLeftButtonUp;
    }

    private static bool IsInteractiveChildForNodeDrag(DependencyObject? source)
    {
        var current = source;
        while (current is not null)
        {
            if (current is TextBox or PasswordBox or ComboBox or ComboBoxItem or ButtonBase or Thumb)
                return true;
            if (current is FrameworkElement fe && fe.DataContext is AutomationNodePortViewModel)
                return true;
            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private bool TryHandleNodePointerDown(
        AutomationCanvasNodeViewModel node,
        DependencyObject? originalSource,
        Point canvasPoint)
    {
        if (WorkspaceVm is null)
            return false;

        if (_isConnectionDragActive)
            EndConnectionDrag();

        if (_isSelectionMarqueeActive)
            EndSelectionRectangle();

        if (IsInteractiveChildForNodeDrag(originalSource))
        {
            WorkspaceVm.SelectNodeForPointerDown(
                node,
                toggleSelection: Keyboard.Modifiers.HasFlag(ModifierKeys.Control));
            return false;
        }

        WorkspaceVm.SelectNodeForPointerDown(
            node,
            toggleSelection: Keyboard.Modifiers.HasFlag(ModifierKeys.Control));
        AutomationRoot.Focus();

        if (_isConnectionDragActive || _isMiddlePanning || _isSelectionMarqueeActive)
            return false;

        StartNodeDrag(node, ToLogicalCanvasPoint(canvasPoint));
        return true;
    }

    private double GetCurrentZoom()
    {
        var zoom = WorkspaceVm?.Zoom ?? 1d;
        return Math.Clamp(zoom, 0.01d, 32d);
    }

    private void AutomationWorkspaceView_NodeDragPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isNodeDragActive)
            return;

        TryUpdateNodeDrag(e);
    }

    private void AutomationWorkspaceView_NodeDragPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isNodeDragActive)
            return;

        EndNodeDrag();
        e.Handled = true;
    }

    private void TryUpdateNodeDrag(MouseEventArgs e)
    {
        if (!_isNodeDragActive || WorkspaceVm is null || _draggingNode is null)
            return;

        if (Mouse.LeftButton != MouseButtonState.Pressed)
        {
            EndNodeDrag();
            return;
        }

        var nextPoint = ToLogicalCanvasPoint(e.GetPosition(CanvasSurface));
        var dx = nextPoint.X - _nodeDragLastCanvasPoint.X;
        var dy = nextPoint.Y - _nodeDragLastCanvasPoint.Y;
        _nodeDragLastCanvasPoint = nextPoint;
        if (Math.Abs(dx) < 0.001d && Math.Abs(dy) < 0.001d)
            return;

        var suppressSnap = Keyboard.Modifiers.HasFlag(ModifierKeys.Alt);
        WorkspaceVm.DragNode(_draggingNode, dx, dy, suppressSnap);
        e.Handled = true;
    }

    private void BeginSelectionRectangle()
    {
        if (_isSelectionMarqueeActive)
            return;

        _isSelectionMarqueeActive = true;
        SelectionRectOverlay.Visibility = Visibility.Visible;
        Canvas.SetLeft(SelectionRectOverlay, _selectionStartCanvasPoint.X);
        Canvas.SetTop(SelectionRectOverlay, _selectionStartCanvasPoint.Y);
        SelectionRectOverlay.Width = 0;
        SelectionRectOverlay.Height = 0;
        Mouse.Capture(this, CaptureMode.SubTree);
    }

    private void AutomationWorkspaceView_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateOverviewViewportFromScroll();
    }

    private void CanvasScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        UpdateOverviewViewportFromScroll();
    }

    private void UpdateOverviewViewportFromScroll()
    {
        if (WorkspaceVm is null)
            return;

        var zoom = GetCurrentZoom();
        var viewportLeft = CanvasScrollViewer.HorizontalOffset / zoom;
        var viewportTop = CanvasScrollViewer.VerticalOffset / zoom;
        var viewportWidth = CanvasScrollViewer.ViewportWidth / zoom;
        var viewportHeight = CanvasScrollViewer.ViewportHeight / zoom;
        WorkspaceVm.SetViewportRect(viewportLeft, viewportTop, viewportWidth, viewportHeight);
    }

    private void MiniMapViewportHost_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (WorkspaceVm is null || MiniMapViewportHost.ActualWidth <= 0 || MiniMapViewportHost.ActualHeight <= 0)
            return;

        _isMiniMapDragging = true;
        Mouse.Capture(MiniMapViewportHost, CaptureMode.Element);
        CenterViewportOnMiniMapPoint(e.GetPosition(MiniMapViewportHost));
        e.Handled = true;
    }

    private void MiniMapViewportHost_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isMiniMapDragging || e.LeftButton != MouseButtonState.Pressed)
            return;

        CenterViewportOnMiniMapPoint(e.GetPosition(MiniMapViewportHost));
        e.Handled = true;
    }

    private void MiniMapViewportHost_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isMiniMapDragging)
            return;

        _isMiniMapDragging = false;
        if (Mouse.Captured == MiniMapViewportHost)
            Mouse.Capture(null);
        e.Handled = true;
    }

    private void CenterViewportOnMiniMapPoint(Point miniMapPoint)
    {
        if (WorkspaceVm is null || MiniMapViewportHost.ActualWidth <= 0 || MiniMapViewportHost.ActualHeight <= 0)
            return;

        var ratioX = Math.Clamp(miniMapPoint.X / MiniMapViewportHost.ActualWidth, 0d, 1d);
        var ratioY = Math.Clamp(miniMapPoint.Y / MiniMapViewportHost.ActualHeight, 0d, 1d);
        var targetX = ratioX * WorkspaceVm.CanvasLogicalWidth;
        var targetY = ratioY * WorkspaceVm.CanvasLogicalHeight;
        var zoom = GetCurrentZoom();
        var halfViewportWidth = CanvasScrollViewer.ViewportWidth / (2d * zoom);
        var halfViewportHeight = CanvasScrollViewer.ViewportHeight / (2d * zoom);
        var left = Math.Clamp(targetX - halfViewportWidth, 0d, WorkspaceVm.CanvasLogicalWidth);
        var top = Math.Clamp(targetY - halfViewportHeight, 0d, WorkspaceVm.CanvasLogicalHeight);
        CanvasScrollViewer.ScrollToHorizontalOffset(left * zoom);
        CanvasScrollViewer.ScrollToVerticalOffset(top * zoom);
        UpdateOverviewViewportFromScroll();
    }

    private void OpenNodeContextMenu(FrameworkElement placementTarget, AutomationCanvasNodeViewModel node)
    {
        if (WorkspaceVm is null)
            return;

        var actions = WorkspaceVm.BuildNodeContextMenuActions(node);
        if (actions.Count == 0)
            return;

        var menu = new ContextMenu
        {
            PlacementTarget = placementTarget
        };
        ApplyNodeContextMenuResources(menu);

        foreach (var action in actions)
        {
            var menuItem = new MenuItem
            {
                Header = AppUiLocalization.GetString(action.LabelResourceKey),
                IsEnabled = action.IsEnabled
            };
            ApplyNodeContextMenuItemStyle(menuItem);
            var capturedKind = action.Kind;
            menuItem.Click += (_, _) => WorkspaceVm.ExecuteNodeContextMenuAction(node, capturedKind);
            menu.Items.Add(menuItem);
        }

        menu.IsOpen = true;
    }

    private static void ApplyNodeContextMenuResources(ContextMenu menu)
    {
        var app = Application.Current;
        if (app is null)
            return;

        if (app.TryFindResource("TrayNotifyContextMenuStyle") is Style menuStyle)
            menu.Style = menuStyle;
    }

    private static void ApplyNodeContextMenuItemStyle(MenuItem item)
    {
        if (Application.Current?.TryFindResource("TrayNotifyMenuItemStyle") is Style style)
            item.Style = style;
    }
}
