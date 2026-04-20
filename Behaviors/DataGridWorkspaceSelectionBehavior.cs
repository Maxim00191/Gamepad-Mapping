#nullable enable
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using Gamepad_Mapping.ViewModels;
using GamepadMapperGUI.Models;

namespace Gamepad_Mapping.Behaviors;

/// <summary>Which profile workspace list a <see cref="DataGrid"/> is editing (for multi-select ↔ VM sync).</summary>
public enum WorkspaceRuleListKind
{
    Mappings = 1,
    KeyboardActions = 2,
    RadialMenus = 3
}

/// <summary>
/// Keeps <see cref="DataGrid.SelectedItems"/> in sync with workspace selection collections on the active view model
/// so Ctrl+A and multi-row selection are reflected in copy/paste commands.
/// </summary>
public static class DataGridWorkspaceSelectionBehavior
{
    private sealed class Bridge
    {
        public required WorkspaceRuleListKind Kind;
        public bool SyncingFromGrid;
        public bool SyncingFromVm;
        public NotifyCollectionChangedEventHandler? CollectionHandler;
        public SelectionChangedEventHandler? SelectionHandler;
        public RoutedEventHandler? LoadedHandler;
        public RoutedEventHandler? UnloadedHandler;
    }

    private static readonly ConditionalWeakTable<DataGrid, Bridge> Bridges = new();

    public static readonly DependencyProperty RuleListKindProperty =
        DependencyProperty.RegisterAttached(
            "RuleListKind",
            typeof(WorkspaceRuleListKind?),
            typeof(DataGridWorkspaceSelectionBehavior),
            new PropertyMetadata(null, OnRuleListKindChanged));

    public static void SetRuleListKind(DataGrid element, WorkspaceRuleListKind? value) =>
        element.SetValue(RuleListKindProperty, value);

    public static WorkspaceRuleListKind? GetRuleListKind(DataGrid element) =>
        (WorkspaceRuleListKind?)element.GetValue(RuleListKindProperty);

    private static void OnRuleListKindChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid grid)
            return;

        Detach(grid);

        if (e.NewValue is WorkspaceRuleListKind kind)
            Attach(grid, kind);
    }

    private static void Attach(DataGrid grid, WorkspaceRuleListKind kind)
    {
        var bridge = new Bridge { Kind = kind };
        Bridges.Add(grid, bridge);

        void Loaded(object _, RoutedEventArgs __) => Wire(grid, bridge);
        bridge.LoadedHandler = Loaded;
        grid.Loaded += Loaded;

        void Unloaded(object _, RoutedEventArgs __) => Detach(grid);
        bridge.UnloadedHandler = Unloaded;
        grid.Unloaded += Unloaded;

        if (grid.IsLoaded)
            Wire(grid, bridge);
    }

    private static void Detach(DataGrid grid)
    {
        if (!Bridges.TryGetValue(grid, out var bridge))
            return;

        Unwire(grid, bridge);

        if (bridge.LoadedHandler is not null)
            grid.Loaded -= bridge.LoadedHandler;
        if (bridge.UnloadedHandler is not null)
            grid.Unloaded -= bridge.UnloadedHandler;

        Bridges.Remove(grid);
    }

    private static void Wire(DataGrid grid, Bridge bridge)
    {
        Unwire(grid, bridge);

        var collection = ResolveCollection(grid, bridge.Kind);
        if (collection is null)
            return;

        bridge.SelectionHandler = (_, _) => OnGridSelectionChanged(grid, bridge);
        grid.SelectionChanged += bridge.SelectionHandler;

        bridge.CollectionHandler = (_, _) =>
        {
            if (bridge.SyncingFromGrid)
                return;
            bridge.SyncingFromVm = true;
            try
            {
                PushVmSelectionToGrid(grid, collection);
            }
            finally
            {
                bridge.SyncingFromVm = false;
            }
        };

        collection.CollectionChanged += bridge.CollectionHandler;
    }

    private static void Unwire(DataGrid grid, Bridge bridge)
    {
        if (bridge.SelectionHandler is not null)
        {
            grid.SelectionChanged -= bridge.SelectionHandler;
            bridge.SelectionHandler = null;
        }

        if (bridge.CollectionHandler is not null)
        {
            var collection = ResolveCollection(grid, bridge.Kind);
            if (collection is not null)
                collection.CollectionChanged -= bridge.CollectionHandler;
            bridge.CollectionHandler = null;
        }
    }

    private static INotifyCollectionChanged? ResolveCollection(DataGrid grid, WorkspaceRuleListKind kind) =>
        kind switch
        {
            WorkspaceRuleListKind.Mappings when grid.DataContext is MappingEditorViewModel m => m.WorkspaceSelectedMappings,
            WorkspaceRuleListKind.KeyboardActions when grid.DataContext is ProfileCatalogPanelViewModel c =>
                c.WorkspaceSelectedKeyboardActions,
            WorkspaceRuleListKind.RadialMenus when grid.DataContext is ProfileCatalogPanelViewModel c2 =>
                c2.WorkspaceSelectedRadialMenus,
            _ => null
        };

    private static void OnGridSelectionChanged(DataGrid grid, Bridge bridge)
    {
        if (bridge.SyncingFromVm)
            return;

        bridge.SyncingFromGrid = true;
        try
        {
            var items = grid.SelectedItems.Cast<object>().ToList();
            switch (bridge.Kind)
            {
                case WorkspaceRuleListKind.Mappings when grid.DataContext is MappingEditorViewModel mm:
                    mm.NotifyWorkspaceMappingSelectionFromGrid(items);
                    break;
                case WorkspaceRuleListKind.KeyboardActions when grid.DataContext is ProfileCatalogPanelViewModel cc:
                    cc.NotifyWorkspaceKeyboardSelectionFromGrid(items);
                    break;
                case WorkspaceRuleListKind.RadialMenus when grid.DataContext is ProfileCatalogPanelViewModel c2:
                    c2.NotifyWorkspaceRadialSelectionFromGrid(items);
                    break;
            }
        }
        finally
        {
            bridge.SyncingFromGrid = false;
        }
    }

    private static void PushVmSelectionToGrid(DataGrid grid, INotifyCollectionChanged n)
    {
        if (n is not IList list)
            return;

        grid.UnselectAll();
        foreach (var item in list)
            grid.SelectedItems.Add(item);
    }
}
