#nullable enable
using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Gamepad_Mapping.ViewModels;
using Gamepad_Mapping.Views;

namespace Gamepad_Mapping.Behaviors;

/// <summary>
/// Routes workspace template checkpoints to <see cref="MainViewModel"/> for snapshot-based undo/redo.
/// </summary>
internal static class WorkspaceEditHistoryRecorder
{
    public static void RecordCheckpointIfPossible(DependencyObject? scopeForLookup)
    {
        if (TryFindMainViewModel(scopeForLookup, out var main))
            main.RecordTemplateWorkspaceCheckpoint();
    }

    public static bool TryFindMainViewModel(DependencyObject? start, out MainViewModel main)
    {
        main = null!;
        for (var d = start; d is not null; d = VisualTreeHelper.GetParent(d))
        {
            if (d is MainView { DataContext: MainViewModel m })
            {
                main = m;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// True if <paramref name="candidate"/> is in the visual subtree of <paramref name="subtreeRoot"/>
    /// or in a <see cref="Popup"/> whose placement target lies under <paramref name="subtreeRoot"/>.
    /// </summary>
    public static bool IsAssociatedWithSubtree(DependencyObject subtreeRoot, DependencyObject? candidate)
    {
        if (candidate is null)
            return false;

        if (IsDescendantOf(subtreeRoot, candidate))
            return true;

        for (var n = candidate; n is not null; n = VisualTreeHelper.GetParent(n))
        {
            if (n is Popup { PlacementTarget: UIElement target } && IsDescendantOf(subtreeRoot, target))
                return true;
        }

        return false;
    }

    private static bool IsDescendantOf(DependencyObject? ancestor, DependencyObject? node)
    {
        while (node is not null)
        {
            if (ReferenceEquals(node, ancestor))
                return true;
            node = VisualTreeHelper.GetParent(node);
        }

        return false;
    }
}

/// <summary>
/// Records a template checkpoint when the user begins editing a <see cref="DataGrid"/> cell,
/// so inline TwoWay bindings participate in undo/redo.
/// </summary>
public static class DataGridWorkspaceEditHistoryBehavior
{
    private sealed class Bridge
    {
        public EventHandler<DataGridBeginningEditEventArgs>? BeginningEditHandler;
    }

    private static readonly ConditionalWeakTable<DataGrid, Bridge> Bridges = new();

    public static readonly DependencyProperty RecordCheckpointOnBeginningEditProperty =
        DependencyProperty.RegisterAttached(
            "RecordCheckpointOnBeginningEdit",
            typeof(bool),
            typeof(DataGridWorkspaceEditHistoryBehavior),
            new PropertyMetadata(false, OnRecordCheckpointOnBeginningEditChanged));

    public static void SetRecordCheckpointOnBeginningEdit(DataGrid element, bool value) =>
        element.SetValue(RecordCheckpointOnBeginningEditProperty, value);

    public static bool GetRecordCheckpointOnBeginningEdit(DataGrid element) =>
        (bool)element.GetValue(RecordCheckpointOnBeginningEditProperty);

    private static void OnRecordCheckpointOnBeginningEditChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid grid)
            return;

        Detach(grid);

        if (e.NewValue is true)
            Attach(grid);
    }

    private static void Attach(DataGrid grid)
    {
        var bridge = new Bridge();
        Bridges.Add(grid, bridge);

        bridge.BeginningEditHandler = (_, _) => WorkspaceEditHistoryRecorder.RecordCheckpointIfPossible(grid);
        grid.BeginningEdit += bridge.BeginningEditHandler;
    }

    private static void Detach(DataGrid grid)
    {
        if (!Bridges.TryGetValue(grid, out var bridge))
            return;

        if (bridge.BeginningEditHandler is not null)
            grid.BeginningEdit -= bridge.BeginningEditHandler;

        Bridges.Remove(grid);
    }
}

/// <summary>
/// Records a checkpoint when keyboard focus moves from outside a panel into it (or an anchored popup),
/// covering detail editors that use TwoWay bindings without a single explicit Save command.
/// </summary>
public static class FrameworkElementWorkspaceEditHistoryBehavior
{
    private sealed class Bridge
    {
        public KeyboardFocusChangedEventHandler? FocusHandler;
    }

    private static readonly ConditionalWeakTable<FrameworkElement, Bridge> Bridges = new();

    public static readonly DependencyProperty RecordCheckpointWhenFocusEntersFromOutsideProperty =
        DependencyProperty.RegisterAttached(
            "RecordCheckpointWhenFocusEntersFromOutside",
            typeof(bool),
            typeof(FrameworkElementWorkspaceEditHistoryBehavior),
            new PropertyMetadata(false, OnRecordCheckpointWhenFocusEntersFromOutsideChanged));

    public static void SetRecordCheckpointWhenFocusEntersFromOutside(FrameworkElement element, bool value) =>
        element.SetValue(RecordCheckpointWhenFocusEntersFromOutsideProperty, value);

    public static bool GetRecordCheckpointWhenFocusEntersFromOutside(FrameworkElement element) =>
        (bool)element.GetValue(RecordCheckpointWhenFocusEntersFromOutsideProperty);

    private static void OnRecordCheckpointWhenFocusEntersFromOutsideChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
            return;

        Detach(element);

        if (e.NewValue is true)
            Attach(element);
    }

    private static void Attach(FrameworkElement scope)
    {
        var bridge = new Bridge();
        Bridges.Add(scope, bridge);

        bridge.FocusHandler = (_, e) => OnPreviewGotKeyboardFocus(scope, e);
        scope.PreviewGotKeyboardFocus += bridge.FocusHandler;
    }

    private static void Detach(FrameworkElement scope)
    {
        if (!Bridges.TryGetValue(scope, out var bridge))
            return;

        if (bridge.FocusHandler is not null)
            scope.PreviewGotKeyboardFocus -= bridge.FocusHandler;

        Bridges.Remove(scope);
    }

    private static void OnPreviewGotKeyboardFocus(FrameworkElement scope, KeyboardFocusChangedEventArgs e)
    {
        var oldIn = WorkspaceEditHistoryRecorder.IsAssociatedWithSubtree(scope, e.OldFocus as DependencyObject);
        var newIn = WorkspaceEditHistoryRecorder.IsAssociatedWithSubtree(scope, e.NewFocus as DependencyObject);

        if (oldIn || !newIn)
            return;

        WorkspaceEditHistoryRecorder.RecordCheckpointIfPossible(scope);
    }
}
