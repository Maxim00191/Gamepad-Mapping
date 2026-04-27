#nullable enable

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Models.Automation;
using GamepadMapperGUI.Services.Automation;
using GamepadMapperGUI.Services.Infrastructure;
using GamepadMapperGUI.Utils;
using Microsoft.Win32;

namespace Gamepad_Mapping.ViewModels;

public partial class AutomationWorkspaceViewModel : ObservableObject
{
    public const double NodeVisualWidth = 280;
    public const double NodeVisualMinHeight = 186;
    public const double NodeVisualMaxWidth = 520;
    public const double NodePortHandleSize = 10;
    public const double NodePortHitSize = 24;
    private const double ConnectionSnapRadiusPixels = 30;

    private readonly INodeTypeRegistry _registry;
    private readonly IAutomationGraphSerializer _serializer;
    private readonly IAutomationTopologyAnalyzer _topology;
    private readonly IAutomationUndoCoordinator _undo;
    private readonly IUserDialogService _dialogs;
    private readonly IAppToastService _toast;
    private readonly IAutomationScreenCaptureService _screenCapture;
    private readonly IAutomationRegionPickerService _regionPicker;
    private readonly IAutomationGraphSmokeRunner _smokeRunner;
    private readonly IAutomationConnectionPolicy _connectionPolicy;
    private readonly IAutomationNodeInlineEditorSchemaService _inlineEditorSchema;
    private readonly IAutomationEdgeGeometryBuilder _edgeGeometryBuilder;
    private readonly IAutomationPortLabelService _portLabelService;
    private readonly IAutomationNodeLayoutMetricsService _nodeLayoutMetricsService;
    private readonly IAutomationOutputActionSelectionService _outputActionSelectionService;

    private AutomationGraphDocument _document = new();
    private Guid? _dragUndoSessionNodeId;
    private readonly HashSet<Guid> _selectedNodeIds = [];
    private readonly HashSet<string> _favoriteNodeTypeIds = [];
    private readonly LinkedList<string> _recentNodeTypeIds = [];
    private const int MaxRecentNodeTypes = 6;

    private readonly Dictionary<Guid, AutomationCanvasNodeViewModel> _nodeVmById = [];
    private readonly Dictionary<string, (double X, double Y)> _portAnchorsByKey = [];
    private readonly Dictionary<string, (double OffsetX, double OffsetY)> _portAnchorOffsetsByKey = [];
    private readonly Dictionary<Guid, (double X, double Y)> _nodeDragRawPositions = [];
    private AutomationConnectionDragState? _connectionDrag;
    public AutomationWorkspaceViewModel(
        INodeTypeRegistry registry,
        IAutomationGraphSerializer serializer,
        IAutomationTopologyAnalyzer topology,
        IAutomationUndoCoordinator undo,
        IUserDialogService dialogs,
        IAppToastService toast,
        IAutomationScreenCaptureService screenCapture,
        IAutomationRegionPickerService regionPicker,
        IAutomationGraphSmokeRunner smokeRunner,
        IAutomationConnectionPolicy connectionPolicy,
        IAutomationNodeInlineEditorSchemaService inlineEditorSchema,
        IAutomationEdgeGeometryBuilder edgeGeometryBuilder,
        IAutomationPortLabelService portLabelService,
        IAutomationNodeLayoutMetricsService nodeLayoutMetricsService,
        IAutomationOutputActionSelectionService outputActionSelectionService)
    {
        _registry = registry;
        _serializer = serializer;
        _topology = topology;
        _undo = undo;
        _dialogs = dialogs;
        _toast = toast;
        _screenCapture = screenCapture;
        _regionPicker = regionPicker;
        _smokeRunner = smokeRunner;
        _connectionPolicy = connectionPolicy;
        _inlineEditorSchema = inlineEditorSchema;
        _edgeGeometryBuilder = edgeGeometryBuilder;
        _portLabelService = portLabelService;
        _nodeLayoutMetricsService = nodeLayoutMetricsService;
        _outputActionSelectionService = outputActionSelectionService;

        CanvasNodes.CollectionChanged += OnCanvasNodesCollectionChanged;
        BuildPalette();
        RebuildFromDocument();
    }

    public ObservableCollection<AutomationNodePaletteItemViewModel> PaletteItems { get; } = [];

    public ObservableCollection<AutomationCanvasNodeViewModel> CanvasNodes { get; } = [];

    public ObservableCollection<AutomationEdgeDisplayViewModel> EdgeDisplays { get; } = [];

    [ObservableProperty]
    private AutomationCanvasNodeViewModel? _selectedNode;

    partial void OnSelectedNodeChanged(AutomationCanvasNodeViewModel? value)
    {
        if (value is not null && !_selectedNodeIds.Contains(value.Id))
        {
            _selectedNodeIds.Clear();
            _selectedNodeIds.Add(value.Id);
        }

        ApplySelectionVisualStates();
        DeleteSelectedCommand.NotifyCanExecuteChanged();
        PickCaptureRegionCommand.NotifyCanExecuteChanged();
        ClearCaptureRegionCommand.NotifyCanExecuteChanged();
        RefreshRoiThumbnail(value);
    }

    [ObservableProperty]
    private double _zoom = 1;

    [ObservableProperty]
    private bool _gridSnapEnabled = true;

    [ObservableProperty]
    private double _snapCellSize = 16;

    [ObservableProperty]
    private string _topologyBannerText = "";

    [ObservableProperty]
    private bool _showMinimap;

    [ObservableProperty]
    private bool _isConnectionPreviewVisible;

    [ObservableProperty]
    private double _connectionPreviewFromX;

    [ObservableProperty]
    private double _connectionPreviewFromY;

    [ObservableProperty]
    private double _connectionPreviewToX;

    [ObservableProperty]
    private double _connectionPreviewToY;

    [ObservableProperty]
    private string _connectionPreviewPathData = "";

    [ObservableProperty]
    private ImageSource? _roiInspectorThumbnail;

    [ObservableProperty]
    private string _automationRunLog = "";

    private void OnCanvasNodesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        ShowMinimap = CanvasNodes.Count >= 8;

    private void BuildPalette()
    {
        var existingFavorites = PaletteItems
            .Where(p => p.IsFavorite)
            .Select(p => p.NodeTypeId)
            .ToArray();
        foreach (var id in existingFavorites)
            _favoriteNodeTypeIds.Add(id);

        PaletteItems.Clear();
        foreach (var def in _registry.AllDefinitions)
        {
            PaletteItems.Add(new AutomationNodePaletteItemViewModel
            {
                NodeTypeId = def.Id,
                CategoryTitle = ResolvePaletteGroupTitle(def.Id),
                DisplayTitle = Local(def.DisplayNameResourceKey),
                Summary = Local(def.SummaryResourceKey),
                Glyph = def.GlyphFontGlyph,
                IsFavorite = _favoriteNodeTypeIds.Contains(def.Id),
                IsRecent = _recentNodeTypeIds.Contains(def.Id)
            });
        }

        SortPalette();
    }

    private void SortPalette()
    {
        var ordered = PaletteItems
            .OrderBy(i => GetPaletteGroupOrder(i.CategoryTitle))
            .ThenByDescending(i => i.IsFavorite)
            .ThenByDescending(i => i.IsRecent)
            .ThenBy(i => i.DisplayTitle, StringComparer.OrdinalIgnoreCase)
            .ToList();

        PaletteItems.Clear();
        foreach (var item in ordered)
            PaletteItems.Add(item);
    }

    private bool CanUndoAction() => _undo.CanUndo;

    [RelayCommand(CanExecute = nameof(CanUndoAction))]
    private void UndoAction()
    {
        var current = _serializer.Serialize(_document);
        if (!_undo.TryUndo(ref current))
            return;

        _document = _serializer.Deserialize(current);
        RebuildFromDocument();
        NotifyUndoRedoCommands();
    }

    private bool CanRedoAction() => _undo.CanRedo;

    [RelayCommand(CanExecute = nameof(CanRedoAction))]
    private void RedoAction()
    {
        var current = _serializer.Serialize(_document);
        if (!_undo.TryRedo(ref current))
            return;

        _document = _serializer.Deserialize(current);
        RebuildFromDocument();
        NotifyUndoRedoCommands();
    }

    private void NotifyUndoRedoCommands()
    {
        UndoActionCommand.NotifyCanExecuteChanged();
        RedoActionCommand.NotifyCanExecuteChanged();
    }

    private void PushUndoCheckpoint()
    {
        _undo.PushCheckpoint(_serializer.Serialize(_document));
        NotifyUndoRedoCommands();
    }

    [RelayCommand]
    private void ImportGraph()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "JSON (*.json)|*.json",
            InitialDirectory = AppPaths.GetAutomationWorkspaceStorageDirectory()
        };

        if (dlg.ShowDialog() != true)
            return;

        try
        {
            var json = File.ReadAllText(dlg.FileName);
            var next = _serializer.Deserialize(json);
            PushUndoCheckpoint();
            _document = next;
            RebuildFromDocument();
            _toast.ShowInfo("AutomationWorkspace_ImportTitle", "AutomationWorkspace_ImportOk");
        }
        catch (Exception ex)
        {
            _dialogs.ShowError(string.Format(Local("AutomationWorkspace_ImportFailedFormat"), ex.Message),
                Local("AutomationWorkspace_ImportTitle"));
        }
    }

    [RelayCommand]
    private void ExportGraph()
    {
        var dlg = new SaveFileDialog
        {
            Filter = "JSON (*.json)|*.json",
            FileName = "automation-graph.json",
            InitialDirectory = AppPaths.GetAutomationWorkspaceStorageDirectory()
        };

        if (dlg.ShowDialog() != true)
            return;

        try
        {
            File.WriteAllText(dlg.FileName, _serializer.Serialize(_document));
            _toast.ShowInfo("AutomationWorkspace_ExportTitle", "AutomationWorkspace_ExportOk");
        }
        catch (Exception ex)
        {
            _dialogs.ShowError(string.Format(Local("AutomationWorkspace_ExportFailedFormat"), ex.Message),
                Local("AutomationWorkspace_ExportTitle"));
        }
    }

    [RelayCommand]
    private void AddNode(string? nodeTypeId)
    {
        if (string.IsNullOrWhiteSpace(nodeTypeId))
            return;

        if (!_registry.TryGet(nodeTypeId, out _))
            return;

        var x = 120 + _document.Nodes.Count * 24;
        var y = 120 + _document.Nodes.Count * 18;
        AddNodeAt(nodeTypeId, x, y);
    }

    public void AddNodeAtViewportCenter(string nodeTypeId, double viewportCenterX, double viewportCenterY)
    {
        AddNodeAt(
            nodeTypeId,
            viewportCenterX - (NodeVisualWidth / 2d),
            viewportCenterY - (NodeVisualMinHeight / 2d));
    }

    public void AddNodeAt(string nodeTypeId, double x, double y, bool bypassSnap = false)
    {
        if (!_registry.TryGet(nodeTypeId, out _))
            return;

        PushUndoCheckpoint();
        var (sx, sy) = ApplySnap(x, y, bypassSnap);
        var state = new AutomationNodeState
        {
            Id = Guid.NewGuid(),
            NodeTypeId = nodeTypeId,
            X = sx,
            Y = sy,
            Properties = new System.Text.Json.Nodes.JsonObject()
        };

        _document.Nodes.Add(state);
        CreateAndRegisterNodeVm(state);
        MarkNodeTypeRecent(nodeTypeId);
        RefreshEdgeDisplays();
    }

    [RelayCommand]
    private void ToggleFavoriteNodeType(string? nodeTypeId)
    {
        if (string.IsNullOrWhiteSpace(nodeTypeId))
            return;

        if (_favoriteNodeTypeIds.Contains(nodeTypeId))
            _favoriteNodeTypeIds.Remove(nodeTypeId);
        else
            _favoriteNodeTypeIds.Add(nodeTypeId);

        var item = PaletteItems.FirstOrDefault(p => p.NodeTypeId == nodeTypeId);
        if (item is not null)
            item.IsFavorite = _favoriteNodeTypeIds.Contains(nodeTypeId);

        SortPalette();
    }

    private void MarkNodeTypeRecent(string nodeTypeId)
    {
        var existing = _recentNodeTypeIds.Find(nodeTypeId);
        if (existing is not null)
            _recentNodeTypeIds.Remove(existing);

        _recentNodeTypeIds.AddFirst(nodeTypeId);
        while (_recentNodeTypeIds.Count > MaxRecentNodeTypes)
            _recentNodeTypeIds.RemoveLast();

        foreach (var item in PaletteItems)
            item.IsRecent = _recentNodeTypeIds.Contains(item.NodeTypeId);

        SortPalette();
    }

    private bool CanDeleteSelected() => _selectedNodeIds.Count > 0;

    [RelayCommand(CanExecute = nameof(CanDeleteSelected))]
    private void DeleteSelected()
    {
        if (_selectedNodeIds.Count == 0)
            return;

        PushUndoCheckpoint();
        var ids = _selectedNodeIds.ToArray();
        _document.Nodes.RemoveAll(n => ids.Contains(n.Id));
        _document.Edges.RemoveAll(e => ids.Contains(e.SourceNodeId) || ids.Contains(e.TargetNodeId));
        foreach (var id in ids)
        {
            if (_nodeVmById.TryGetValue(id, out var vm))
                CanvasNodes.Remove(vm);
            _nodeVmById.Remove(id);
        }

        _selectedNodeIds.Clear();
        SelectedNode = null;
        ApplySelectionVisualStates();
        RefreshEdgeDisplays();
    }

    [RelayCommand]
    private void SelectNode(AutomationCanvasNodeViewModel? node) => SelectSingleNode(node);

    public void SelectSingleNode(AutomationCanvasNodeViewModel? node)
    {
        _selectedNodeIds.Clear();
        if (node is not null)
            _selectedNodeIds.Add(node.Id);

        SelectedNode = node;
        ApplySelectionVisualStates();
        DeleteSelectedCommand.NotifyCanExecuteChanged();
    }

    public void ToggleNodeSelection(AutomationCanvasNodeViewModel node)
    {
        if (_selectedNodeIds.Contains(node.Id))
            _selectedNodeIds.Remove(node.Id);
        else
            _selectedNodeIds.Add(node.Id);

        SelectedNode = _selectedNodeIds.Count > 0 &&
                       _selectedNodeIds.Contains(node.Id)
            ? node
            : CanvasNodes.FirstOrDefault(n => _selectedNodeIds.Contains(n.Id));
        ApplySelectionVisualStates();
        DeleteSelectedCommand.NotifyCanExecuteChanged();
    }

    public void SetNodeHover(Guid nodeId, bool isHovered)
    {
        if (_nodeVmById.TryGetValue(nodeId, out var node))
            node.IsHovered = isHovered;
    }

    public void SetPortHover(Guid nodeId, string portId, bool isOutputPort, bool isHovered)
    {
        if (!_nodeVmById.TryGetValue(nodeId, out var node))
            return;

        var ports = isOutputPort ? node.OutputPorts : node.InputPorts;
        var port = ports.FirstOrDefault(p => string.Equals(p.Id, portId, StringComparison.Ordinal));
        if (port is not null)
            port.IsHovered = isHovered;
    }

    public void SelectNodesInRectangle(double x1, double y1, double x2, double y2, bool additive)
    {
        var left = Math.Min(x1, x2);
        var top = Math.Min(y1, y2);
        var right = Math.Max(x1, x2);
        var bottom = Math.Max(y1, y2);

        if (!additive)
            _selectedNodeIds.Clear();

        foreach (var node in CanvasNodes)
        {
            var nodeLeft = node.X;
            var nodeTop = node.Y;
            var nodeRight = node.X + node.NodeVisualWidth;
            var nodeBottom = node.Y + node.EstimatedVisualHeight;
            var intersects = nodeRight >= left &&
                             nodeLeft <= right &&
                             nodeBottom >= top &&
                             nodeTop <= bottom;
            if (intersects)
                _selectedNodeIds.Add(node.Id);
        }

        SelectedNode = CanvasNodes.FirstOrDefault(n => _selectedNodeIds.Contains(n.Id));
        ApplySelectionVisualStates();
        DeleteSelectedCommand.NotifyCanExecuteChanged();
    }

    public bool DisconnectPortConnections(Guid nodeId, string portId, bool isOutputPort)
    {
        var hasConnection = isOutputPort
            ? _document.Edges.Any(e =>
                e.SourceNodeId == nodeId &&
                string.Equals(e.SourcePortId, portId, StringComparison.Ordinal))
            : _document.Edges.Any(e =>
                e.TargetNodeId == nodeId &&
                string.Equals(e.TargetPortId, portId, StringComparison.Ordinal));
        if (!hasConnection)
            return false;

        PushUndoCheckpoint();
        var removed = isOutputPort
            ? _document.Edges.RemoveAll(e =>
                e.SourceNodeId == nodeId &&
                string.Equals(e.SourcePortId, portId, StringComparison.Ordinal))
            : _document.Edges.RemoveAll(e =>
                e.TargetNodeId == nodeId &&
                string.Equals(e.TargetPortId, portId, StringComparison.Ordinal));

        if (removed <= 0)
            return false;

        RefreshEdgeDisplays();
        return true;
    }

    public bool DisconnectEdge(Guid edgeId)
    {
        var hasEdge = _document.Edges.Any(e => e.Id == edgeId);
        if (!hasEdge)
            return false;

        PushUndoCheckpoint();
        var removed = _document.Edges.RemoveAll(e => e.Id == edgeId);
        if (removed <= 0)
            return false;
        RefreshEdgeDisplays();
        return true;
    }

    public void BeginNodeMoveSession(AutomationCanvasNodeViewModel node)
    {
        if (_dragUndoSessionNodeId == node.Id)
            return;

        PushUndoCheckpoint();
        _dragUndoSessionNodeId = node.Id;
        _nodeDragRawPositions[node.Id] = (node.X, node.Y);
    }

    public void EndNodeMoveSession(AutomationCanvasNodeViewModel node)
    {
        if (_dragUndoSessionNodeId == node.Id)
            _dragUndoSessionNodeId = null;
        _nodeDragRawPositions.Remove(node.Id);
    }

    public void DragNode(AutomationCanvasNodeViewModel node, double dx, double dy, bool suppressSnap)
    {
        if (!_nodeDragRawPositions.TryGetValue(node.Id, out var raw))
            raw = (node.X, node.Y);

        var nx = raw.X + dx;
        var ny = raw.Y + dy;
        _nodeDragRawPositions[node.Id] = (nx, ny);
        var (sx, sy) = ApplySnap(nx, ny, suppressSnap);
        node.X = sx;
        node.Y = sy;
    }

    private (double X, double Y) ApplySnap(double x, double y, bool bypassSnap)
    {
        if (!GridSnapEnabled || bypassSnap || SnapCellSize <= 1)
            return (x, y);

        var g = SnapCellSize;
        return (Math.Round(x / g) * g, Math.Round(y / g) * g);
    }

    private void RebuildFromDocument()
    {
        foreach (var vm in CanvasNodes.ToList())
            vm.PositionChanged -= OnNodePositionChanged;

        CanvasNodes.Clear();
        _nodeVmById.Clear();
        _portAnchorsByKey.Clear();
        _portAnchorOffsetsByKey.Clear();

        foreach (var state in _document.Nodes)
            CreateAndRegisterNodeVm(state);

        _selectedNodeIds.RemoveWhere(id => !_nodeVmById.ContainsKey(id));
        SelectedNode = _selectedNodeIds.Count > 0
            ? CanvasNodes.FirstOrDefault(n => _selectedNodeIds.Contains(n.Id))
            : null;
        ApplySelectionVisualStates();
        RefreshEdgeDisplays();
    }

    private void CreateAndRegisterNodeVm(AutomationNodeState state)
    {
        var def = _registry.GetRequired(state.NodeTypeId);
        var inputPorts = def.InputPorts
            .Select(p => new AutomationNodePortViewModel
            {
                Id = p.Id,
                DisplayLabel = Local(_portLabelService.ResolveDisplayNameResourceKey(p.Id, p.IsOutput, p.FlowKind)),
                IsOutput = p.IsOutput,
                FlowKind = p.FlowKind,
                PortType = p.PortType,
                BaseStroke = AutomationPortVisualPalette.GetBaseStroke(p.FlowKind, p.PortType),
                BaseFill = AutomationPortVisualPalette.GetBaseFill(p.FlowKind, p.PortType, p.IsOutput)
            })
            .ToArray();
        var outputPorts = def.OutputPorts
            .Select(p => new AutomationNodePortViewModel
            {
                Id = p.Id,
                DisplayLabel = Local(_portLabelService.ResolveDisplayNameResourceKey(p.Id, p.IsOutput, p.FlowKind)),
                IsOutput = p.IsOutput,
                FlowKind = p.FlowKind,
                PortType = p.PortType,
                BaseStroke = AutomationPortVisualPalette.GetBaseStroke(p.FlowKind, p.PortType),
                BaseFill = AutomationPortVisualPalette.GetBaseFill(p.FlowKind, p.PortType, p.IsOutput)
            })
            .ToArray();
        var vm = new AutomationCanvasNodeViewModel(
            state,
            Local(def.DisplayNameResourceKey),
            def.GlyphFontGlyph,
            inputPorts,
            outputPorts);
        PopulateInlineEditors(vm);
        vm.ApplyLayoutMetrics(_nodeLayoutMetricsService.Build(
            vm.InputPorts.Select(p => p.DisplayLabel).ToArray(),
            vm.OutputPorts.Select(p => p.DisplayLabel).ToArray(),
            vm.InlineEditors.Count));
        vm.PositionChanged += OnNodePositionChanged;
        _nodeVmById[state.Id] = vm;
        CanvasNodes.Add(vm);
    }

    private void PopulateInlineEditors(AutomationCanvasNodeViewModel node)
    {
        node.InlineEditors.Clear();
        var props = node.State.Properties;
        foreach (var definition in _inlineEditorSchema.GetDefinitions(node.NodeTypeId))
        {
            var item = new AutomationInlineNodeFieldViewModel
            {
                NodeId = node.Id,
                NodeTypeId = node.NodeTypeId,
                PropertyKey = definition.PropertyKey,
                Label = Local(definition.LabelResourceKey),
                Kind = definition.Kind,
                Placeholder = definition.PlaceholderResourceKey is not null
                    ? Local(definition.PlaceholderResourceKey)
                    : "",
                ActionKind = definition.ActionKind,
                ActionLabel = ResolveInlineActionLabel(node, definition)
            };
            switch (definition.Kind)
            {
                case AutomationNodeInlineEditorKind.Action:
                {
                    break;
                }
                case AutomationNodeInlineEditorKind.Boolean:
                {
                    item.BooleanValue = ReadBooleanDefaulted(props, definition);
                    break;
                }
                case AutomationNodeInlineEditorKind.Integer:
                {
                    item.TextValue = ReadIntegerDefaulted(props, definition).ToString();
                    break;
                }
                case AutomationNodeInlineEditorKind.Double:
                {
                    var doubleValue = ReadDoubleDefaulted(props, definition);
                    item.TextValue = doubleValue.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
                    break;
                }
                default:
                {
                    item.TextValue = ReadStringDefaulted(props, definition);
                    break;
                }
            }

            node.InlineEditors.Add(item);
        }

        node.ApplyLayoutMetrics(_nodeLayoutMetricsService.Build(
            node.InputPorts.Select(p => p.DisplayLabel).ToArray(),
            node.OutputPorts.Select(p => p.DisplayLabel).ToArray(),
            node.InlineEditors.Count));
    }

    private string ResolveInlineActionLabel(
        AutomationCanvasNodeViewModel node,
        AutomationNodeInlineEditorDefinition definition)
    {
        if (definition.ActionKind == AutomationNodeInlineEditorActionKind.PickKeyboardActionId)
        {
            var actionId = AutomationNodePropertyReader.ReadString(node.State.Properties, AutomationNodePropertyKeys.KeyboardActionId);
            return _outputActionSelectionService.BuildKeyboardActionPickerDisplayText(actionId);
        }

        if (definition.ActionKind == AutomationNodeInlineEditorActionKind.PickMouseActionId)
        {
            var actionId = AutomationNodePropertyReader.ReadString(node.State.Properties, AutomationNodePropertyKeys.MouseActionId);
            return _outputActionSelectionService.BuildMouseActionPickerDisplayText(actionId);
        }

        return definition.ActionLabelResourceKey is not null
            ? Local(definition.ActionLabelResourceKey)
            : "";
    }

    private void OnNodePositionChanged(object? sender, EventArgs e)
    {
        if (sender is AutomationCanvasNodeViewModel node)
            SyncNodeAnchorsFromOffsets(node);
        RefreshEdgeDisplays();
    }

    private void RefreshEdgeDisplays()
    {
        EdgeDisplays.Clear();
        var analysis = _topology.Analyze(_document);
        var cycleSet = analysis.CycleEdgeIds.Count > 0
            ? new HashSet<Guid>(analysis.CycleEdgeIds)
            : null;

        foreach (var edge in _document.Edges)
        {
            if (!_nodeVmById.TryGetValue(edge.SourceNodeId, out var sVm))
                continue;
            if (!_nodeVmById.TryGetValue(edge.TargetNodeId, out var tVm))
                continue;

            if (!TryGetPortAnchor(sVm.Id, edge.SourcePortId, true, out var fx, out var fy))
                continue;
            if (!TryGetPortAnchor(tVm.Id, edge.TargetPortId, false, out var tx, out var ty))
                continue;

            EdgeDisplays.Add(new AutomationEdgeDisplayViewModel(_edgeGeometryBuilder)
            {
                EdgeId = edge.Id,
                SourcePortId = edge.SourcePortId,
                TargetPortId = edge.TargetPortId,
                FromX = fx,
                FromY = fy,
                ToX = tx,
                ToY = ty,
                IsCycleWarning = cycleSet?.Contains(edge.Id) == true
            });
        }

        TopologyBannerText = analysis.DetailMessageResourceKey is not null
            ? Local(analysis.DetailMessageResourceKey)
            : "";
    }

    public void BeginConnectionDrag(Guid startNodeId, string startPortId, bool startPortIsOutput)
    {
        if (!TryGetPortAnchor(startNodeId, startPortId, startPortIsOutput, out var x, out var y))
            return;

        _connectionDrag = new AutomationConnectionDragState
        {
            StartNodeId = startNodeId,
            StartPortId = startPortId,
            StartPortIsOutput = startPortIsOutput
        };

        ConnectionPreviewFromX = x;
        ConnectionPreviewFromY = y;
        ConnectionPreviewToX = x;
        ConnectionPreviewToY = y;
        IsConnectionPreviewVisible = true;
        RebuildConnectionPreviewPathData();
        ResetAllPortVisualStates();
    }

    public void UpdateConnectionDrag(
        double x,
        double y,
        Guid? hoverNodeId,
        string? hoverPortId,
        bool? hoverPortIsOutput)
    {
        if (_connectionDrag is null || !IsConnectionPreviewVisible)
            return;

        var hasExplicitPort = hoverNodeId is not null &&
                              !string.IsNullOrWhiteSpace(hoverPortId) &&
                              hoverPortIsOutput is not null;
        if (TryResolveConnectionTarget(
                x,
                y,
                hasExplicitPort ? hoverNodeId : null,
                hasExplicitPort ? hoverPortId : null,
                hasExplicitPort ? hoverPortIsOutput : null,
                out var targetNodeId,
                out var targetPortId,
                out var targetPortIsOutput,
                out var targetX,
                out var targetY,
                out var validation))
        {
            _connectionDrag.HoverNodeId = targetNodeId;
            _connectionDrag.HoverPortId = targetPortId;
            _connectionDrag.HoverPortIsOutput = targetPortIsOutput;
            _connectionDrag.HoverValidationAllowed = validation.IsAllowed;
            _connectionDrag.HoverValidationReasonResourceKey = validation.ReasonResourceKey;
            ConnectionPreviewToX = targetX;
            ConnectionPreviewToY = targetY;
        }
        else
        {
            _connectionDrag.HoverNodeId = null;
            _connectionDrag.HoverPortId = null;
            _connectionDrag.HoverPortIsOutput = null;
            _connectionDrag.HoverValidationAllowed = false;
            _connectionDrag.HoverValidationReasonResourceKey = null;
            ConnectionPreviewToX = x;
            ConnectionPreviewToY = y;
        }

        RebuildConnectionPreviewPathData();
        UpdatePortVisualFeedback();
    }

    public bool CompleteConnectionDrag()
    {
        if (_connectionDrag is null)
            return false;

        var drag = _connectionDrag;
        if (drag.HoverNodeId is null || string.IsNullOrWhiteSpace(drag.HoverPortId) || drag.HoverPortIsOutput is null)
            return false;
        if (!drag.HoverValidationAllowed)
        {
            var msg = string.IsNullOrWhiteSpace(drag.HoverValidationReasonResourceKey)
                ? Local("AutomationConnection_GenericRejected")
                : Local(drag.HoverValidationReasonResourceKey!);
            _dialogs.ShowWarning(msg, Local("AutomationWorkspace_LinkRejectedTitle"));
            return false;
        }

        var (sourceNodeId, sourcePortId, targetNodeId, targetPortId) = BuildConnectionEndpoints(
            drag.StartNodeId,
            drag.StartPortId,
            drag.StartPortIsOutput,
            drag.HoverNodeId.Value,
            drag.HoverPortId!,
            drag.HoverPortIsOutput.Value);
        return TryCreateConnection(sourceNodeId, sourcePortId!, targetNodeId, targetPortId!);
    }

    public void CancelConnectionDrag()
    {
        _connectionDrag = null;
        IsConnectionPreviewVisible = false;
        ConnectionPreviewPathData = "";
        ResetAllPortVisualStates();
    }

    private bool TryCreateConnection(Guid sourceNodeId, string sourcePortId, Guid targetNodeId, string targetPortId)
    {
        var validation = _topology.ValidateConnection(_document, sourceNodeId, sourcePortId, targetNodeId, targetPortId);
        if (!validation.IsAllowed)
        {
            var msg = validation.ReasonResourceKey is null
                ? Local("AutomationConnection_GenericRejected")
                : Local(validation.ReasonResourceKey);
            _dialogs.ShowWarning(msg, Local("AutomationWorkspace_LinkRejectedTitle"));
            return false;
        }

        var sourceNode = _document.Nodes.FirstOrDefault(n => n.Id == sourceNodeId);
        var targetNode = _document.Nodes.FirstOrDefault(n => n.Id == targetNodeId);
        PushUndoCheckpoint();

        if (validation.ExistingIncomingEdgeId is Guid existingEdgeId &&
            sourceNode is not null &&
            targetNode is not null &&
            _registry.TryGet(sourceNode.NodeTypeId, out var sourceDef) &&
            _registry.TryGet(targetNode.NodeTypeId, out var targetDef))
        {
            var outPort = sourceDef?.OutputPorts.FirstOrDefault(p => string.Equals(p.Id, sourcePortId, StringComparison.Ordinal));
            var inPort = targetDef?.InputPorts.FirstOrDefault(p => string.Equals(p.Id, targetPortId, StringComparison.Ordinal));
            if (outPort is not null && inPort is not null)
            {
                if (_connectionPolicy.ShouldReplaceIncomingConnection(outPort, inPort))
                {
                    _document.Edges.RemoveAll(e => e.Id == existingEdgeId);
                }
                else
                {
                    _dialogs.ShowWarning(Local("AutomationConnection_TargetInputAlreadyConnected"),
                        Local("AutomationWorkspace_LinkRejectedTitle"));
                    return false;
                }
            }
        }

        _document.Edges.Add(new AutomationEdgeState
        {
            Id = Guid.NewGuid(),
            SourceNodeId = sourceNodeId,
            SourcePortId = sourcePortId,
            TargetNodeId = targetNodeId,
            TargetPortId = targetPortId
        });
        RefreshEdgeDisplays();
        return true;
    }

    public bool TryGetPortAnchor(Guid nodeId, string portId, bool isOutputPort, out double x, out double y)
    {
        x = 0;
        y = 0;
        var key = BuildPortAnchorKey(nodeId, portId, isOutputPort);
        if (_portAnchorOffsetsByKey.TryGetValue(key, out var offset) &&
            _nodeVmById.TryGetValue(nodeId, out var node))
        {
            x = node.X + offset.OffsetX;
            y = node.Y + offset.OffsetY;
            return true;
        }

        if (_portAnchorsByKey.TryGetValue(key, out var anchor))
        {
            x = anchor.X;
            y = anchor.Y;
            return true;
        }

        return false;
    }

    public void UpdatePortAnchor(Guid nodeId, string portId, bool isOutputPort, double x, double y)
    {
        var key = BuildPortAnchorKey(nodeId, portId, isOutputPort);
        if (_portAnchorsByKey.TryGetValue(key, out var current) &&
            Math.Abs(current.X - x) < 0.25d &&
            Math.Abs(current.Y - y) < 0.25d)
        {
            return;
        }

        _portAnchorsByKey[key] = (x, y);
        if (_nodeVmById.TryGetValue(nodeId, out var node))
            _portAnchorOffsetsByKey[key] = (x - node.X, y - node.Y);
        SyncConnectionPreviewAnchors();
        RefreshEdgeDisplays();
    }

    public void ClearPortAnchor(Guid nodeId, string portId, bool isOutputPort)
    {
        var key = BuildPortAnchorKey(nodeId, portId, isOutputPort);
        var removedAbsolute = _portAnchorsByKey.Remove(key);
        var removedOffset = _portAnchorOffsetsByKey.Remove(key);
        if (removedAbsolute || removedOffset)
        {
            SyncConnectionPreviewAnchors();
            RefreshEdgeDisplays();
        }
    }

    private void SyncNodeAnchorsFromOffsets(AutomationCanvasNodeViewModel node)
    {
        foreach (var port in node.InputPorts)
            SyncPortAnchorFromOffset(node, port.Id, isOutputPort: false);

        foreach (var port in node.OutputPorts)
            SyncPortAnchorFromOffset(node, port.Id, isOutputPort: true);
    }

    private void SyncPortAnchorFromOffset(AutomationCanvasNodeViewModel node, string portId, bool isOutputPort)
    {
        var key = BuildPortAnchorKey(node.Id, portId, isOutputPort);
        if (!_portAnchorOffsetsByKey.TryGetValue(key, out var offset))
            return;

        _portAnchorsByKey[key] = (node.X + offset.OffsetX, node.Y + offset.OffsetY);
    }

    private void UpdatePortVisualFeedback()
    {
        ResetAllPortVisualStates();
        if (_connectionDrag is null)
            return;

        if (!_nodeVmById.TryGetValue(_connectionDrag.StartNodeId, out var startNodeVm))
            return;
        if (!_registry.TryGet(startNodeVm.NodeTypeId, out var startNodeDef) || startNodeDef is null)
            return;

        var startPort = ResolvePortDescriptor(
            startNodeDef,
            _connectionDrag.StartPortId,
            _connectionDrag.StartPortIsOutput);
        if (startPort is null)
            return;

        foreach (var candidateNode in CanvasNodes)
        {
            var candidatePorts = _connectionDrag.StartPortIsOutput ? candidateNode.InputPorts : candidateNode.OutputPorts;
            foreach (var candidatePort in candidatePorts)
            {
                if (!_registry.TryGet(candidateNode.NodeTypeId, out var candidateDef) || candidateDef is null)
                    continue;

                var candidateDescriptor = ResolvePortDescriptor(
                    candidateDef,
                    candidatePort.Id,
                    isOutputPort: !_connectionDrag.StartPortIsOutput);
                if (candidateDescriptor is null)
                    continue;

                var quickTypeCompatible =
                    startPort.FlowKind == candidateDescriptor.FlowKind &&
                    AutomationPortCompatibility.TypesMatch(startPort.PortType, candidateDescriptor.PortType) &&
                    candidateNode.Id != _connectionDrag.StartNodeId;

                if (!quickTypeCompatible)
                {
                    candidatePort.VisualState = AutomationPortVisualState.CandidateInvalid;
                    continue;
                }

                var sourceNodeId = _connectionDrag.StartPortIsOutput ? _connectionDrag.StartNodeId : candidateNode.Id;
                var sourcePortId = _connectionDrag.StartPortIsOutput ? _connectionDrag.StartPortId : candidatePort.Id;
                var targetNodeId = _connectionDrag.StartPortIsOutput ? candidateNode.Id : _connectionDrag.StartNodeId;
                var targetPortId = _connectionDrag.StartPortIsOutput ? candidatePort.Id : _connectionDrag.StartPortId;
                var validation = _topology.ValidateConnection(_document, sourceNodeId, sourcePortId, targetNodeId, targetPortId);
                candidatePort.VisualState = validation.IsAllowed
                    ? AutomationPortVisualState.CandidateValid
                    : AutomationPortVisualState.CandidateInvalid;
            }
        }

        if (_connectionDrag.HoverNodeId is null || string.IsNullOrWhiteSpace(_connectionDrag.HoverPortId))
            return;
        if (!_nodeVmById.TryGetValue(_connectionDrag.HoverNodeId.Value, out var hoverNode))
            return;

        var hoverPorts = _connectionDrag.HoverPortIsOutput == true ? hoverNode.OutputPorts : hoverNode.InputPorts;
        var hoverPort = hoverPorts.FirstOrDefault(p => string.Equals(p.Id, _connectionDrag.HoverPortId, StringComparison.Ordinal));
        if (hoverPort is null)
            return;

        if (_connectionDrag.StartPortIsOutput == hoverPort.IsOutput)
        {
            hoverPort.VisualState = AutomationPortVisualState.CandidateInvalid;
            return;
        }
    }

    private bool TryResolveConnectionTarget(
        double pointerX,
        double pointerY,
        Guid? explicitHoverNodeId,
        string? explicitHoverPortId,
        bool? explicitHoverPortIsOutput,
        out Guid hoverNodeId,
        out string hoverPortId,
        out bool hoverPortIsOutput,
        out double hoverAnchorX,
        out double hoverAnchorY,
        out ConnectionValidationResult validationResult)
    {
        hoverNodeId = Guid.Empty;
        hoverPortId = "";
        hoverPortIsOutput = false;
        hoverAnchorX = pointerX;
        hoverAnchorY = pointerY;
        validationResult = new ConnectionValidationResult(false, null);
        if (_connectionDrag is null)
            return false;

        if (explicitHoverNodeId is Guid directNodeId &&
            !string.IsNullOrWhiteSpace(explicitHoverPortId) &&
            explicitHoverPortIsOutput is bool directIsOutput &&
            directIsOutput != _connectionDrag.StartPortIsOutput &&
            TryGetPortAnchor(directNodeId, explicitHoverPortId, directIsOutput, out var explicitX, out var explicitY))
        {
            var (sourceNodeId, sourcePortId, targetNodeId, targetPortId) = BuildConnectionEndpoints(
                _connectionDrag.StartNodeId,
                _connectionDrag.StartPortId,
                _connectionDrag.StartPortIsOutput,
                directNodeId,
                explicitHoverPortId,
                directIsOutput);
            var explicitValidation = _topology.ValidateConnection(
                _document,
                sourceNodeId,
                sourcePortId,
                targetNodeId,
                targetPortId);
            hoverNodeId = directNodeId;
            hoverPortId = explicitHoverPortId;
            hoverPortIsOutput = directIsOutput;
            hoverAnchorX = explicitX;
            hoverAnchorY = explicitY;
            validationResult = explicitValidation;
            return true;
        }

        var targetWantsOutput = !_connectionDrag.StartPortIsOutput;
        var currentZoom = Math.Max(Zoom, 0.01d);
        var snapRadiusLogical = ConnectionSnapRadiusPixels / currentZoom;
        var bestDistanceSq = snapRadiusLogical * snapRadiusLogical;
        var hasBest = false;
        foreach (var node in CanvasNodes)
        {
            var candidatePorts = targetWantsOutput ? node.OutputPorts : node.InputPorts;
            foreach (var port in candidatePorts)
            {
                if (node.Id == _connectionDrag.StartNodeId &&
                    string.Equals(port.Id, _connectionDrag.StartPortId, StringComparison.Ordinal) &&
                    port.IsOutput == _connectionDrag.StartPortIsOutput)
                {
                    continue;
                }

                if (!TryGetPortAnchor(node.Id, port.Id, port.IsOutput, out var anchorX, out var anchorY))
                    continue;

                var dx = anchorX - pointerX;
                var dy = anchorY - pointerY;
                var distanceSq = (dx * dx) + (dy * dy);
                if (distanceSq > bestDistanceSq)
                    continue;

                var (sourceNodeId, sourcePortId, targetNodeId, targetPortId) = BuildConnectionEndpoints(
                    _connectionDrag.StartNodeId,
                    _connectionDrag.StartPortId,
                    _connectionDrag.StartPortIsOutput,
                    node.Id,
                    port.Id,
                    port.IsOutput);
                var candidateValidation = _topology.ValidateConnection(_document, sourceNodeId, sourcePortId, targetNodeId, targetPortId);
                bestDistanceSq = distanceSq;
                hoverNodeId = node.Id;
                hoverPortId = port.Id;
                hoverPortIsOutput = port.IsOutput;
                hoverAnchorX = anchorX;
                hoverAnchorY = anchorY;
                validationResult = candidateValidation;
                hasBest = true;
            }
        }

        return hasBest;
    }

    private static (Guid SourceNodeId, string SourcePortId, Guid TargetNodeId, string TargetPortId) BuildConnectionEndpoints(
        Guid startNodeId,
        string startPortId,
        bool startPortIsOutput,
        Guid hoverNodeId,
        string hoverPortId,
        bool hoverPortIsOutput)
    {
        if (startPortIsOutput == hoverPortIsOutput)
            return (startNodeId, startPortId, hoverNodeId, hoverPortId);

        return startPortIsOutput
            ? (startNodeId, startPortId, hoverNodeId, hoverPortId)
            : (hoverNodeId, hoverPortId, startNodeId, startPortId);
    }

    private void ResetAllPortVisualStates()
    {
        foreach (var node in CanvasNodes)
        {
            foreach (var port in node.InputPorts)
                port.VisualState = AutomationPortVisualState.Default;
            foreach (var port in node.OutputPorts)
                port.VisualState = AutomationPortVisualState.Default;
        }
    }

    private void ApplySelectionVisualStates()
    {
        foreach (var node in CanvasNodes)
        {
            node.VisualState = _selectedNodeIds.Contains(node.Id)
                ? AutomationCanvasVisualState.Selected
                : AutomationCanvasVisualState.Default;
        }
    }

    private static string BuildPortAnchorKey(Guid nodeId, string portId, bool isOutputPort) =>
        $"{nodeId:N}|{(isOutputPort ? "o" : "i")}|{portId}";

    private static AutomationPortDescriptor? ResolvePortDescriptor(
        AutomationNodeTypeDefinition nodeDefinition,
        string portId,
        bool isOutputPort)
    {
        var ports = isOutputPort ? nodeDefinition.OutputPorts : nodeDefinition.InputPorts;
        return ports.FirstOrDefault(p => string.Equals(p.Id, portId, StringComparison.Ordinal));
    }

    partial void OnConnectionPreviewFromXChanged(double value) => RebuildConnectionPreviewPathData();
    partial void OnConnectionPreviewFromYChanged(double value) => RebuildConnectionPreviewPathData();
    partial void OnConnectionPreviewToXChanged(double value) => RebuildConnectionPreviewPathData();
    partial void OnConnectionPreviewToYChanged(double value) => RebuildConnectionPreviewPathData();

    private void RebuildConnectionPreviewPathData()
    {
        ConnectionPreviewPathData = IsConnectionPreviewVisible
            ? _edgeGeometryBuilder.BuildPathData(ConnectionPreviewFromX, ConnectionPreviewFromY, ConnectionPreviewToX, ConnectionPreviewToY)
            : "";
    }

    private void SyncConnectionPreviewAnchors()
    {
        if (_connectionDrag is null || !IsConnectionPreviewVisible)
            return;

        if (TryGetPortAnchor(
                _connectionDrag.StartNodeId,
                _connectionDrag.StartPortId,
                _connectionDrag.StartPortIsOutput,
                out var startX,
                out var startY))
        {
            ConnectionPreviewFromX = startX;
            ConnectionPreviewFromY = startY;
        }

        if (_connectionDrag.HoverNodeId is not Guid hoverNodeId ||
            string.IsNullOrWhiteSpace(_connectionDrag.HoverPortId) ||
            _connectionDrag.HoverPortIsOutput is not bool hoverIsOutput)
        {
            return;
        }

        if (TryGetPortAnchor(hoverNodeId, _connectionDrag.HoverPortId!, hoverIsOutput, out var hoverX, out var hoverY))
        {
            ConnectionPreviewToX = hoverX;
            ConnectionPreviewToY = hoverY;
        }
    }

    private bool CanPickCaptureRoi() =>
        SelectedNode is not null &&
        string.Equals(SelectedNode.NodeTypeId, "perception.capture_screen", StringComparison.OrdinalIgnoreCase);

    [RelayCommand(CanExecute = nameof(CanPickCaptureRoi))]
    private async Task PickCaptureRegionAsync()
    {
        if (SelectedNode is not null)
            await PickCaptureRegionForNodeAsync(SelectedNode);
    }

    private async Task PickCaptureRegionForNodeAsync(AutomationCanvasNodeViewModel node)
    {
        try
        {
            var rect = await _regionPicker.PickRectanglePhysicalAsync();
            if (rect is not { } r || r.IsEmpty)
                return;

            PushUndoCheckpoint();
            var st = node.State;
            st.Properties ??= new JsonObject();
            st.Properties[AutomationNodePropertyKeys.CaptureMode] = "roi";
            st.Properties[AutomationNodePropertyKeys.CoordinateSpace] = "physical";
            st.Properties[AutomationNodePropertyKeys.CaptureRoi] = new JsonObject
            {
                ["x"] = r.X,
                ["y"] = r.Y,
                ["width"] = r.Width,
                ["height"] = r.Height
            };

            var thumbBmp = _screenCapture.CaptureRectanglePhysical(r.X, r.Y, r.Width, r.Height);
            st.Properties[AutomationNodePropertyKeys.CaptureRoiThumbnailBase64] =
                AutomationThumbnailEncoder.ToPngBase64(thumbBmp, 96);

            var cachePath = Path.Combine(AppPaths.GetAutomationCaptureCacheDirectory(), $"{node.Id:N}.png");
            try
            {
                SavePng(thumbBmp, cachePath);
                st.Properties[AutomationNodePropertyKeys.CaptureRoiCachePath] = cachePath;
            }
            catch
            {
            }

            PopulateInlineEditors(node);
            if (SelectedNode?.Id == node.Id)
                RefreshRoiThumbnail(node);
            _toast.ShowInfo("AutomationWorkspace_RoiSavedTitle", "AutomationWorkspace_RoiSavedOk");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _dialogs.ShowError(ex.Message, Local("AutomationWorkspace_RoiPickTitle"));
        }
    }

    private static void SavePng(BitmapSource bmp, string path)
    {
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        using var fs = File.Create(path);
        enc.Save(fs);
    }

    [RelayCommand]
    private async Task RunSmokeOnceAsync()
    {
        AppendAutomationLog(Local("AutomationSmoke_Start"));
        try
        {
            var result = await _smokeRunner.RunOnceAsync(_document);
            foreach (var line in result.LogLines)
                AppendAutomationLog(line);

            var msg = result.MessageResourceKey is not null ? Local(result.MessageResourceKey) : "";
            if (!string.IsNullOrEmpty(msg))
                AppendAutomationLog(msg);
            if (!string.IsNullOrEmpty(result.Detail))
                AppendAutomationLog(result.Detail);

            if (result.Ok)
                _toast.ShowInfo("AutomationSmoke_Title", "AutomationSmoke_CompletedToast");
            else
                _dialogs.ShowWarning(string.IsNullOrEmpty(msg) ? Local("AutomationSmoke_Title") : msg,
                    Local("AutomationSmoke_Title"));
        }
        catch (Exception ex)
        {
            AppendAutomationLog(ex.Message);
            _dialogs.ShowError(ex.Message, Local("AutomationSmoke_Title"));
        }
    }

    [RelayCommand]
    private void CommitInlineNodeField(AutomationInlineNodeFieldViewModel? field)
    {
        if (field is null)
            return;
        if (!_nodeVmById.TryGetValue(field.NodeId, out var node))
            return;

        var definition = _inlineEditorSchema
            .GetDefinitions(node.NodeTypeId)
            .FirstOrDefault(x => string.Equals(x.PropertyKey, field.PropertyKey, StringComparison.Ordinal));
        if (definition is null)
            return;
        if (definition.Kind == AutomationNodeInlineEditorKind.Action)
            return;

        var checkpoint = _serializer.Serialize(_document);
        var props = node.State.Properties ??= new JsonObject();
        if (!TryWriteInlineFieldValue(props, field, definition))
            return;

        _undo.PushCheckpoint(checkpoint);
        NotifyUndoRedoCommands();
        PopulateInlineEditors(node);
        if (SelectedNode?.Id == node.Id)
            RefreshRoiThumbnail(node);
    }

    [RelayCommand]
    private async Task ExecuteInlineNodeActionAsync(AutomationInlineNodeFieldViewModel? field)
    {
        if (field is null)
            return;
        if (!_nodeVmById.TryGetValue(field.NodeId, out var node))
            return;

        switch (field.ActionKind)
        {
            case AutomationNodeInlineEditorActionKind.BrowseImageFile:
            {
                BrowseInlineNeedleImage(field);
                break;
            }
            case AutomationNodeInlineEditorActionKind.PickCaptureRegion:
            {
                await PickCaptureRegionForNodeAsync(node);
                break;
            }
            case AutomationNodeInlineEditorActionKind.ClearCaptureRegion:
            {
                ClearCaptureRegionForNode(node);
                break;
            }
            case AutomationNodeInlineEditorActionKind.PickKeyboardActionId:
            {
                PickKeyboardActionForNode(node);
                break;
            }
            case AutomationNodeInlineEditorActionKind.PickMouseActionId:
            {
                PickMouseActionForNode(node);
                break;
            }
        }
    }

    private void PickKeyboardActionForNode(AutomationCanvasNodeViewModel node)
    {
        var props = node.State.Properties ??= new JsonObject();
        var currentActionId = AutomationNodePropertyReader.ReadString(props, AutomationNodePropertyKeys.KeyboardActionId);
        var selected = _outputActionSelectionService.PickKeyboardActionId(Application.Current?.MainWindow, currentActionId);
        if (selected is null)
            return;

        var nextActionId = selected.Trim();
        var previousActionId = currentActionId?.Trim() ?? string.Empty;
        if (string.Equals(previousActionId, nextActionId, StringComparison.Ordinal))
            return;

        PushUndoCheckpoint();
        AutomationNodePropertyReader.WriteString(props, AutomationNodePropertyKeys.KeyboardActionId, nextActionId);
        if (_outputActionSelectionService.TryResolveKeyboardAction(nextActionId, out var resolvedKey))
            AutomationNodePropertyReader.WriteString(props, AutomationNodePropertyKeys.KeyboardKey, resolvedKey);
        PopulateInlineEditors(node);
    }

    private void PickMouseActionForNode(AutomationCanvasNodeViewModel node)
    {
        var props = node.State.Properties ??= new JsonObject();
        var currentActionId = AutomationNodePropertyReader.ReadString(props, AutomationNodePropertyKeys.MouseActionId);
        var selected = _outputActionSelectionService.PickMouseActionId(Application.Current?.MainWindow, currentActionId);
        if (selected is null)
            return;

        var nextActionId = selected.Trim();
        var previousActionId = currentActionId?.Trim() ?? string.Empty;
        if (string.Equals(previousActionId, nextActionId, StringComparison.Ordinal))
            return;

        PushUndoCheckpoint();
        AutomationNodePropertyReader.WriteString(props, AutomationNodePropertyKeys.MouseActionId, nextActionId);
        if (_outputActionSelectionService.TryResolveMouseAction(nextActionId, out var resolvedAction))
        {
            AutomationNodePropertyReader.WriteString(props, AutomationNodePropertyKeys.MouseActionMode, resolvedAction.ActionMode);
            AutomationNodePropertyReader.WriteString(props, AutomationNodePropertyKeys.MouseButton, resolvedAction.Button);
        }
        PopulateInlineEditors(node);
    }

    [RelayCommand]
    private void BrowseInlineNeedleImage(AutomationInlineNodeFieldViewModel? field)
    {
        if (field is null)
            return;
        if (!string.Equals(field.NodeTypeId, "perception.find_image", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(field.PropertyKey, AutomationNodePropertyKeys.FindImageNeedlePath, StringComparison.Ordinal))
        {
            return;
        }

        var dlg = new OpenFileDialog
        {
            Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|All files (*.*)|*.*",
            InitialDirectory = AppPaths.GetAutomationCaptureCacheDirectory()
        };
        if (dlg.ShowDialog() != true)
            return;

        field.TextValue = dlg.FileName;
        CommitInlineNodeField(field);
    }

    private bool CanClearCaptureRoi() =>
        SelectedNode is not null &&
        string.Equals(SelectedNode.NodeTypeId, "perception.capture_screen", StringComparison.OrdinalIgnoreCase);

    [RelayCommand(CanExecute = nameof(CanClearCaptureRoi))]
    private void ClearCaptureRegion()
    {
        if (SelectedNode is not null)
            ClearCaptureRegionForNode(SelectedNode);
    }

    private void ClearCaptureRegionForNode(AutomationCanvasNodeViewModel node)
    {
        PushUndoCheckpoint();
        var props = node.State.Properties ??= new JsonObject();
        props.Remove(AutomationNodePropertyKeys.CaptureRoi);
        props.Remove(AutomationNodePropertyKeys.CaptureRoiThumbnailBase64);
        props.Remove(AutomationNodePropertyKeys.CaptureRoiCachePath);
        AutomationNodePropertyReader.WriteString(props, AutomationNodePropertyKeys.CaptureMode, "full");
        PopulateInlineEditors(node);
        if (SelectedNode?.Id == node.Id)
            RefreshRoiThumbnail(node);
    }

    private void AppendAutomationLog(string line)
    {
        AutomationRunLog = string.IsNullOrEmpty(AutomationRunLog) ? line : $"{AutomationRunLog}\n{line}";
    }

    private void RefreshRoiThumbnail(AutomationCanvasNodeViewModel? node)
    {
        RoiInspectorThumbnail = null;
        if (node?.State.Properties is null)
            return;

        var b64 = AutomationNodePropertyReader.ReadString(node.State.Properties,
            AutomationNodePropertyKeys.CaptureRoiThumbnailBase64);
        if (string.IsNullOrWhiteSpace(b64))
            return;

        try
        {
            using var ms = new MemoryStream(Convert.FromBase64String(b64));
            var img = new BitmapImage();
            img.BeginInit();
            img.StreamSource = ms;
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.EndInit();
            img.Freeze();
            RoiInspectorThumbnail = img;
        }
        catch
        {
        }
    }

    private static string ReadStringDefaulted(JsonObject? props, AutomationNodeInlineEditorDefinition definition)
    {
        var raw = AutomationNodePropertyReader.ReadString(props, definition.PropertyKey);
        return string.IsNullOrWhiteSpace(raw)
            ? definition.DefaultTextValue
            : raw;
    }

    private static bool ReadBooleanDefaulted(JsonObject? props, AutomationNodeInlineEditorDefinition definition)
    {
        if (string.Equals(definition.PropertyKey, AutomationNodePropertyKeys.CaptureMode, StringComparison.Ordinal))
        {
            var mode = AutomationNodePropertyReader.ReadString(props, definition.PropertyKey);
            return string.Equals(mode, "roi", StringComparison.OrdinalIgnoreCase);
        }

        return props is null || !props.ContainsKey(definition.PropertyKey)
            ? definition.DefaultBooleanValue
            : AutomationNodePropertyReader.ReadBool(props, definition.PropertyKey);
    }

    private static int ReadIntegerDefaulted(JsonObject? props, AutomationNodeInlineEditorDefinition definition)
    {
        var fallback = int.TryParse(definition.DefaultTextValue, out var parsedFallback) ? parsedFallback : 0;
        var value = AutomationNodePropertyReader.ReadInt(props, definition.PropertyKey, fallback);
        if (definition.MinIntegerValue is int min)
            value = Math.Max(value, min);
        if (definition.MaxIntegerValue is int max)
            value = Math.Min(value, max);
        return value;
    }

    private static double ReadDoubleDefaulted(JsonObject? props, AutomationNodeInlineEditorDefinition definition)
    {
        var fallback = double.TryParse(
            definition.DefaultTextValue,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsedFallback)
            ? parsedFallback
            : 0d;
        var value = AutomationNodePropertyReader.ReadDouble(props, definition.PropertyKey, fallback);
        if (definition.MinDoubleValue is double min)
            value = Math.Max(value, min);
        if (definition.MaxDoubleValue is double max)
            value = Math.Min(value, max);
        return value;
    }

    private static bool TryWriteInlineFieldValue(
        JsonObject props,
        AutomationInlineNodeFieldViewModel field,
        AutomationNodeInlineEditorDefinition definition)
    {
        switch (definition.Kind)
        {
            case AutomationNodeInlineEditorKind.Boolean:
            {
                if (string.Equals(definition.PropertyKey, AutomationNodePropertyKeys.CaptureMode, StringComparison.Ordinal))
                {
                    var nextMode = field.BooleanValue ? "roi" : "full";
                    var currentMode = AutomationNodePropertyReader.ReadString(props, definition.PropertyKey);
                    if (string.Equals(currentMode, nextMode, StringComparison.OrdinalIgnoreCase))
                        return false;
                    AutomationNodePropertyReader.WriteString(
                        props,
                        definition.PropertyKey,
                        nextMode);
                }
                else
                {
                    var currentBool = AutomationNodePropertyReader.ReadBool(props, definition.PropertyKey);
                    if (currentBool == field.BooleanValue)
                        return false;
                    AutomationNodePropertyReader.WriteBool(props, definition.PropertyKey, field.BooleanValue);
                }

                return true;
            }
            case AutomationNodeInlineEditorKind.Integer:
            {
                var parsed = int.TryParse(field.TextValue, out var intValue)
                    ? intValue
                    : ReadIntegerDefaulted(props, definition);
                if (definition.MinIntegerValue is int min)
                    parsed = Math.Max(parsed, min);
                if (definition.MaxIntegerValue is int max)
                    parsed = Math.Min(parsed, max);
                var currentInt = AutomationNodePropertyReader.ReadInt(props, definition.PropertyKey, parsed);
                if (currentInt == parsed)
                    return false;
                AutomationNodePropertyReader.WriteInt(props, definition.PropertyKey, parsed);
                return true;
            }
            case AutomationNodeInlineEditorKind.Double:
            {
                var parsed = double.TryParse(
                    field.TextValue,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var doubleValue)
                    ? doubleValue
                    : ReadDoubleDefaulted(props, definition);
                if (definition.MinDoubleValue is double min)
                    parsed = Math.Max(parsed, min);
                if (definition.MaxDoubleValue is double max)
                    parsed = Math.Min(parsed, max);
                var currentDouble = AutomationNodePropertyReader.ReadDouble(props, definition.PropertyKey, parsed);
                if (Math.Abs(currentDouble - parsed) < 0.000001d)
                    return false;
                AutomationNodePropertyReader.WriteDouble(props, definition.PropertyKey, parsed);
                return true;
            }
            default:
            {
                var nextText = field.TextValue.Trim();
                var currentText = AutomationNodePropertyReader.ReadString(props, definition.PropertyKey);
                if (string.Equals(currentText ?? "", nextText, StringComparison.Ordinal))
                    return false;
                AutomationNodePropertyReader.WriteString(props, definition.PropertyKey, nextText);
                return true;
            }
        }
    }

    private static string Local(string key) => AppUiLocalization.GetString(key);

    private static int GetPaletteGroupOrder(string categoryTitle)
    {
        var groups = new[]
        {
            "AutomationWorkspace_Group_Perception",
            "AutomationWorkspace_Group_Action",
            "AutomationWorkspace_Group_ControlFlow",
            "AutomationWorkspace_Group_MathLogic",
            "AutomationWorkspace_Group_Variables",
            "AutomationWorkspace_Group_Debug"
        };

        for (var i = 0; i < groups.Length; i++)
        {
            if (string.Equals(Local(groups[i]), categoryTitle, StringComparison.Ordinal))
                return i;
        }

        return groups.Length;
    }

    private static string ResolvePaletteGroupTitle(string nodeTypeId)
    {
        if (nodeTypeId.StartsWith("perception.", StringComparison.Ordinal))
            return Local("AutomationWorkspace_Group_Perception");
        if (nodeTypeId.StartsWith("output.", StringComparison.Ordinal))
            return Local("AutomationWorkspace_Group_Action");
        if (nodeTypeId.StartsWith("automation.", StringComparison.Ordinal) ||
            nodeTypeId.StartsWith("logic.branch_", StringComparison.Ordinal) ||
            string.Equals(nodeTypeId, "logic.switch", StringComparison.Ordinal) ||
            string.Equals(nodeTypeId, "logic.loop_control", StringComparison.Ordinal))
        {
            return Local("AutomationWorkspace_Group_ControlFlow");
        }

        if (nodeTypeId.StartsWith("math.", StringComparison.Ordinal) ||
            nodeTypeId.StartsWith("control.", StringComparison.Ordinal) ||
            string.Equals(nodeTypeId, "logic.and", StringComparison.Ordinal) ||
            string.Equals(nodeTypeId, "logic.or", StringComparison.Ordinal) ||
            string.Equals(nodeTypeId, "logic.not", StringComparison.Ordinal) ||
            string.Equals(nodeTypeId, "logic.gt", StringComparison.Ordinal) ||
            string.Equals(nodeTypeId, "logic.lt", StringComparison.Ordinal) ||
            string.Equals(nodeTypeId, "logic.eq", StringComparison.Ordinal))
        {
            return Local("AutomationWorkspace_Group_MathLogic");
        }

        if (nodeTypeId.StartsWith("variables.", StringComparison.Ordinal))
            return Local("AutomationWorkspace_Group_Variables");
        if (nodeTypeId.StartsWith("debug.", StringComparison.Ordinal))
            return Local("AutomationWorkspace_Group_Debug");
        return Local("AutomationWorkspace_Group_Other");
    }
}
