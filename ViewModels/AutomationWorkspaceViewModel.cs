#nullable enable

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Models.Automation;
using GamepadMapperGUI.Services.Automation;
using GamepadMapperGUI.Services.Infrastructure;
using GamepadMapperGUI.Utils;
using Gamepad_Mapping.Utils;
using Gamepad_Mapping.Views.Automation;
using Microsoft.Win32;

namespace Gamepad_Mapping.ViewModels;

public partial class AutomationWorkspaceViewModel : ObservableObject
{
    public const double MinimumCanvasLogicalWidth = 4200;
    public const double MinimumCanvasLogicalHeight = 4200;
    public const double NodeVisualWidth = 280;
    public const double NodeVisualMinHeight = 186;
    public const double NodeVisualMaxWidth = 520;
    public const double NodePortHandleSize = 10;
    public const double NodePortHitSize = 24;
    private const double ConnectionSnapRadiusPixels = 30;
    private const double CanvasNodeExtentPadding = 900;

    private readonly INodeTypeRegistry _registry;
    private readonly IAutomationGraphSerializer _serializer;
    private readonly IAutomationTopologyAnalyzer _topology;
    private readonly IAutomationUndoCoordinator _undo;
    private readonly IUserDialogService _dialogs;
    private readonly IAppToastService _toast;
    private readonly IAutomationScreenCaptureServiceResolver _screenCaptureResolver;
    private readonly IAutomationRoiPreviewImageProvider _roiPreviewImageProvider;
    private readonly IAutomationRegionPickerService _regionPicker;
    private readonly IAutomationScriptRunner _scriptRunner;
    private readonly IAutomationConnectionPolicy _connectionPolicy;
    private readonly IAutomationNodeInlineEditorSchemaService _inlineEditorSchema;
    private readonly IAutomationEdgeGeometryBuilder _edgeGeometryBuilder;
    private readonly IAutomationPortLabelService _portLabelService;
    private readonly IAutomationNodeLayoutMetricsService _nodeLayoutMetricsService;
    private readonly IAutomationOutputActionSelectionService _outputActionSelectionService;
    private readonly IAutomationInputModeSelectionService _inputModeSelectionService;
    private readonly IAutomationNodeContextMenuService _nodeContextMenuService;
    private readonly IProcessTargetService? _processTargetService;
    private readonly Dictionary<Guid, Debouncer> _captureProcessTargetDebouncers = [];

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
    private readonly object _runStateSync = new();
    private CancellationTokenSource? _activeRunCts;
    private AutomationConnectionDragState? _connectionDrag;
    private AutomationRoiPreviewWindow? _roiPreviewWindow;
    private DispatcherTimer? _roiInspectorLiveTimer;

    public AutomationWorkspaceViewModel(
        INodeTypeRegistry registry,
        IAutomationGraphSerializer serializer,
        IAutomationTopologyAnalyzer topology,
        IAutomationUndoCoordinator undo,
        IUserDialogService dialogs,
        IAppToastService toast,
        IAutomationScreenCaptureServiceResolver screenCaptureResolver,
        IAutomationRoiPreviewImageProvider roiPreviewImageProvider,
        IAutomationRegionPickerService regionPicker,
        IAutomationScriptRunner scriptRunner,
        IAutomationConnectionPolicy connectionPolicy,
        IAutomationNodeInlineEditorSchemaService inlineEditorSchema,
        IAutomationEdgeGeometryBuilder edgeGeometryBuilder,
        IAutomationPortLabelService portLabelService,
        IAutomationNodeLayoutMetricsService nodeLayoutMetricsService,
        IAutomationOutputActionSelectionService outputActionSelectionService,
        IAutomationInputModeSelectionService inputModeSelectionService,
        IAutomationNodeContextMenuService nodeContextMenuService,
        IProcessTargetService? processTargetService = null)
    {
        _registry = registry;
        _serializer = serializer;
        _topology = topology;
        _undo = undo;
        _dialogs = dialogs;
        _toast = toast;
        _screenCaptureResolver = screenCaptureResolver;
        _roiPreviewImageProvider = roiPreviewImageProvider;
        _regionPicker = regionPicker;
        _scriptRunner = scriptRunner;
        _connectionPolicy = connectionPolicy;
        _inlineEditorSchema = inlineEditorSchema;
        _edgeGeometryBuilder = edgeGeometryBuilder;
        _portLabelService = portLabelService;
        _nodeLayoutMetricsService = nodeLayoutMetricsService;
        _outputActionSelectionService = outputActionSelectionService;
        _inputModeSelectionService = inputModeSelectionService;
        _nodeContextMenuService = nodeContextMenuService;
        _processTargetService = processTargetService;

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
        OpenRoiPreviewWindowCommand.NotifyCanExecuteChanged();
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
    private double _canvasLogicalWidth = MinimumCanvasLogicalWidth;

    [ObservableProperty]
    private double _canvasLogicalHeight = MinimumCanvasLogicalHeight;

    [ObservableProperty]
    private double _overviewViewportLeft;

    [ObservableProperty]
    private double _overviewViewportTop;

    [ObservableProperty]
    private double _overviewViewportWidth = MinimumCanvasLogicalWidth;

    [ObservableProperty]
    private double _overviewViewportHeight = MinimumCanvasLogicalHeight;

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
    private string _roiInspectorSummaryText = "";

    [ObservableProperty]
    private bool _isRoiInspectorLivePreview = true;

    partial void OnIsRoiInspectorLivePreviewChanged(bool value) =>
        RefreshRoiThumbnail(SelectedNode);

    public ObservableCollection<string> AutomationRunLogLines { get; } = [];

    private const int MaxAutomationRunLogDisplayedLines = 8_000;

    [ObservableProperty]
    private bool _isBackgroundCheckRunning;

    partial void OnIsBackgroundCheckRunningChanged(bool value) =>
        RunCurrentScriptInBackgroundCommand.NotifyCanExecuteChanged();

    [ObservableProperty]
    private bool _isAutomationRunInProgress;

    partial void OnIsAutomationRunInProgressChanged(bool value)
    {
        RunSmokeOnceCommand.NotifyCanExecuteChanged();
        RunCurrentScriptInBackgroundCommand.NotifyCanExecuteChanged();
        EmergencyStopCommand.NotifyCanExecuteChanged();
    }

    partial void OnZoomChanged(double value)
    {
        // Keep the viewport values in sync with zoom-dependent dimensions.
        SetViewportRect(OverviewViewportLeft, OverviewViewportTop, OverviewViewportWidth, OverviewViewportHeight);
    }

    partial void OnCanvasLogicalWidthChanged(double value) =>
        SetViewportRect(OverviewViewportLeft, OverviewViewportTop, OverviewViewportWidth, OverviewViewportHeight);

    partial void OnCanvasLogicalHeightChanged(double value) =>
        SetViewportRect(OverviewViewportLeft, OverviewViewportTop, OverviewViewportWidth, OverviewViewportHeight);

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
            InitialDirectory = AppPaths.GetAutomationImportDirectory()
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
            _dialogs.ShowError(string.Format(Local("AutomationWorkspace_ImportFailedFormat"),
                    ExceptionMessageFormatter.UserFacingMessage(ex)),
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
            _dialogs.ShowError(string.Format(Local("AutomationWorkspace_ExportFailedFormat"),
                    ExceptionMessageFormatter.UserFacingMessage(ex)),
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
        RecalculateCanvasBounds();
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
        RecalculateCanvasBounds();
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

    public void SelectNodeForPointerDown(AutomationCanvasNodeViewModel node, bool toggleSelection)
    {
        if (toggleSelection)
        {
            ToggleNodeSelection(node);
            return;
        }

        if (!_selectedNodeIds.Contains(node.Id) || _selectedNodeIds.Count <= 1)
        {
            SelectSingleNode(node);
            return;
        }

        if (!ReferenceEquals(SelectedNode, node))
            SelectedNode = node;
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

    public void SetViewportRect(double viewportLeft, double viewportTop, double viewportWidth, double viewportHeight)
    {
        var clampedWidth = Math.Clamp(viewportWidth, 1d, CanvasLogicalWidth);
        var clampedHeight = Math.Clamp(viewportHeight, 1d, CanvasLogicalHeight);
        var clampedLeft = Math.Clamp(viewportLeft, 0d, CanvasLogicalWidth - clampedWidth);
        var clampedTop = Math.Clamp(viewportTop, 0d, CanvasLogicalHeight - clampedHeight);
        OverviewViewportLeft = clampedLeft;
        OverviewViewportTop = clampedTop;
        OverviewViewportWidth = clampedWidth;
        OverviewViewportHeight = clampedHeight;
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
        _nodeDragRawPositions.Clear();
        foreach (var dragNode in ResolveDragNodes(node))
            _nodeDragRawPositions[dragNode.Id] = (dragNode.X, dragNode.Y);
    }

    public void EndNodeMoveSession(AutomationCanvasNodeViewModel node)
    {
        if (_dragUndoSessionNodeId == node.Id)
            _dragUndoSessionNodeId = null;
        _nodeDragRawPositions.Remove(node.Id);
    }

    public void DragNode(AutomationCanvasNodeViewModel node, double dx, double dy, bool suppressSnap)
    {
        var dragNodes = ResolveDragNodes(node).ToArray();
        if (dragNodes.Length == 0)
            return;

        foreach (var dragNode in dragNodes)
        {
            if (!_nodeDragRawPositions.TryGetValue(dragNode.Id, out var raw))
                raw = (dragNode.X, dragNode.Y);

            var nx = raw.X + dx;
            var ny = raw.Y + dy;
            _nodeDragRawPositions[dragNode.Id] = (nx, ny);
            var (sx, sy) = ApplySnap(nx, ny, suppressSnap);
            dragNode.X = sx;
            dragNode.Y = sy;
        }
    }

    private IEnumerable<AutomationCanvasNodeViewModel> ResolveDragNodes(AutomationCanvasNodeViewModel anchor)
    {
        if (_selectedNodeIds.Count > 1 && _selectedNodeIds.Contains(anchor.Id))
            return CanvasNodes.Where(n => _selectedNodeIds.Contains(n.Id));
        return [anchor];
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
        RecalculateCanvasBounds();
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
        RefreshNodeDisplayMetadata(vm);
        PopulateInlineEditors(vm);
        ScheduleCaptureProcessTargetResolutionForNode(vm);
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
            if (!IsInlineEditorDefinitionVisible(props, definition))
                continue;

            IReadOnlyList<AutomationInlineChoiceItemViewModel> choiceItems = definition is
            { Kind: AutomationNodeInlineEditorKind.Choice, ChoiceOptions: { Count: > 0 } list }
                ? list
                    .Select(o => new AutomationInlineChoiceItemViewModel
                    {
                        Display = Local(o.LabelResourceKey),
                        StoredValue = o.StoredValue
                    })
                    .ToArray()
                : Array.Empty<AutomationInlineChoiceItemViewModel>();

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
                ActionLabel = ResolveInlineActionLabel(node, definition),
                SecondaryActionKind = definition.SecondaryActionKind,
                SecondaryActionLabel = ResolveSecondaryInlineActionLabel(definition),
                ChoiceItems = choiceItems
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
                case AutomationNodeInlineEditorKind.Choice:
                {
                    var raw = AutomationNodePropertyReader.ReadString(props, definition.PropertyKey);
                    item.TextValue = NormalizeInlineChoice(raw, definition);
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

        RefreshNodeDisplayMetadata(node);
        node.ApplyLayoutMetrics(_nodeLayoutMetricsService.Build(
            node.InputPorts.Select(p => p.DisplayLabel).ToArray(),
            node.OutputPorts.Select(p => p.DisplayLabel).ToArray(),
            node.InlineEditors.Count));
    }

    private static bool IsInlineEditorDefinitionVisible(
        JsonObject? props,
        AutomationNodeInlineEditorDefinition definition)
    {
        if (definition.VisibleWhenPropertyKey is not { Length: > 0 } key ||
            definition.VisibleWhenPropertyValues is not { Count: > 0 } allowedValues)
        {
            return true;
        }

        var raw = AutomationNodePropertyReader.ReadString(props, key).Trim();
        return allowedValues.Any(value => string.Equals(value, raw, StringComparison.OrdinalIgnoreCase));
    }

    private static void RefreshNodeDisplayMetadata(AutomationCanvasNodeViewModel node)
    {
        node.ApplyDisplayMetadata(
            AutomationNodePropertyReader.ReadString(node.State.Properties, AutomationNodePropertyKeys.Description));
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

        if (definition.ActionKind == AutomationNodeInlineEditorActionKind.PickInputModeId)
        {
            var inputModeId = AutomationNodePropertyReader.ReadString(node.State.Properties, AutomationNodePropertyKeys.InputEmulationApiId);
            return _inputModeSelectionService.BuildInputModePickerDisplayText(inputModeId);
        }

        return definition.ActionLabelResourceKey is not null
            ? Local(definition.ActionLabelResourceKey)
            : "";
    }

    private string ResolveSecondaryInlineActionLabel(AutomationNodeInlineEditorDefinition definition)
    {
        if (definition.SecondaryActionKind == AutomationNodeInlineEditorActionKind.None)
            return "";
        if (definition.SecondaryActionLabelResourceKey is { } key)
            return Local(key);
        return "";
    }

    private void OnNodePositionChanged(object? sender, EventArgs e)
    {
        if (sender is AutomationCanvasNodeViewModel node)
            SyncNodeAnchorsFromOffsets(node);
        RecalculateCanvasBounds();
        RefreshEdgeDisplays();
    }

    private void RecalculateCanvasBounds()
    {
        var width = MinimumCanvasLogicalWidth;
        var height = MinimumCanvasLogicalHeight;
        foreach (var node in CanvasNodes)
        {
            width = Math.Max(width, node.X + node.NodeVisualWidth + CanvasNodeExtentPadding);
            height = Math.Max(height, node.Y + node.EstimatedVisualHeight + CanvasNodeExtentPadding);
        }

        CanvasLogicalWidth = width;
        CanvasLogicalHeight = height;
    }

    private void RefreshEdgeDisplays()
    {
        EdgeDisplays.Clear();
        var analysis = _topology.Analyze(_document);
        var cycleSet = analysis.DataCycleEdgeIds.Count > 0
            ? new HashSet<Guid>(analysis.DataCycleEdgeIds)
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
        string.Equals(SelectedNode.NodeTypeId, AutomationNodeTypeIds.CaptureScreen, StringComparison.OrdinalIgnoreCase);

    [RelayCommand(CanExecute = nameof(CanPickCaptureRoi))]
    private async Task PickCaptureRegionAsync()
    {
        if (SelectedNode is not null)
            await PickCaptureRegionForNodeAsync(SelectedNode);
    }

    [RelayCommand(CanExecute = nameof(CanPickCaptureRoi))]
    private void OpenRoiPreviewWindow()
    {
        if (Application.Current?.MainWindow is not Window owner)
            return;

        if (_roiPreviewWindow is { IsLoaded: true })
        {
            if (_roiPreviewWindow.DataContext is AutomationRoiPreviewViewModel previewVm)
                previewVm.ReloadFromWorkspace();
            _roiPreviewWindow.Activate();
            return;
        }

        var vm = new AutomationRoiPreviewViewModel(this, _roiPreviewImageProvider);
        var w = new AutomationRoiPreviewWindow
        {
            Owner = owner,
            DataContext = vm
        };
        w.Closed += (_, _) =>
        {
            vm.Detach();
            _roiPreviewWindow = null;
        };
        _roiPreviewWindow = w;
        w.Show();
    }

    private async Task PickCaptureRegionForNodeAsync(AutomationCanvasNodeViewModel node)
    {
        try
        {
            var pick = await _regionPicker.PickRectanglePhysicalAsync();
            if (pick is not { } p || p.Rect.IsEmpty)
                return;
            var r = p.Rect;

            PushUndoCheckpoint();
            var st = node.State;
            st.Properties ??= new JsonObject();
            st.Properties[AutomationNodePropertyKeys.CaptureMode] = AutomationCaptureMode.Roi;
            st.Properties[AutomationNodePropertyKeys.CoordinateSpace] = "physical";
            st.Properties[AutomationNodePropertyKeys.CaptureRoi] = new JsonObject
            {
                ["x"] = r.X,
                ["y"] = r.Y,
                ["width"] = r.Width,
                ["height"] = r.Height
            };

            var thumbBmp = p.CroppedPhysicalBitmap
                ?? _screenCaptureResolver.ResolveForNodeProperties(st.Properties)
                    .CaptureRectanglePhysical(r.X, r.Y, r.Width, r.Height);

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
            _dialogs.ShowError(ExceptionMessageFormatter.UserFacingMessage(ex), Local("AutomationWorkspace_RoiPickTitle"));
        }
    }

    private static void SavePng(BitmapSource bmp, string path)
    {
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        using var fs = File.Create(path);
        enc.Save(fs);
    }

    private bool CanRunSmokeOnce() => !IsAutomationRunInProgress;

    [RelayCommand(CanExecute = nameof(CanRunSmokeOnce))]
    private async Task RunSmokeOnceAsync()
    {
        await ExecuteScriptRunAsync(
            Local("AutomationSmoke_Start"),
            "AutomationSmoke_Title",
            "AutomationSmoke_CompletedToast",
            showFailureDialog: true);
    }

    private bool CanRunCurrentScriptInBackground() =>
        !IsBackgroundCheckRunning && !IsAutomationRunInProgress;

    [RelayCommand(CanExecute = nameof(CanRunCurrentScriptInBackground))]
    private async Task RunCurrentScriptInBackgroundAsync()
    {
        if (IsBackgroundCheckRunning)
            return;

        IsBackgroundCheckRunning = true;
        try
        {
            await ExecuteScriptRunAsync(
                Local("AutomationWorkspace_BackgroundRunStart"),
                "AutomationSmoke_Title",
                "AutomationWorkspace_BackgroundRunCompletedToast",
                showFailureDialog: false);
        }
        finally
        {
            IsBackgroundCheckRunning = false;
        }
    }

    private bool CanEmergencyStop() => IsAutomationRunInProgress;

    [RelayCommand(CanExecute = nameof(CanEmergencyStop))]
    private void EmergencyStop()
    {
        CancellationTokenSource? cts;
        lock (_runStateSync)
            cts = _activeRunCts;

        if (cts is null)
            return;

        AppendAutomationLogLine(Local("AutomationWorkspace_EmergencyStopRequestedLog"));
        cts.Cancel();
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
        ScheduleCaptureProcessTargetResolution(node, definition.PropertyKey);
        if (SelectedNode?.Id == node.Id)
            RefreshRoiThumbnail(node);
    }

    private void ScheduleCaptureProcessTargetResolution(AutomationCanvasNodeViewModel node, string changedPropertyKey)
    {
        if (_processTargetService is null ||
            !string.Equals(node.NodeTypeId, "perception.capture_screen", StringComparison.OrdinalIgnoreCase) ||
            changedPropertyKey is not AutomationNodePropertyKeys.CaptureProcessName
                and not AutomationNodePropertyKeys.CaptureSourceMode
                and not AutomationNodePropertyKeys.CaptureProcessId)
        {
            return;
        }

        ScheduleCaptureProcessTargetResolutionForNode(node);
    }

    private void ScheduleCaptureProcessTargetResolutionForNode(AutomationCanvasNodeViewModel node)
    {
        if (_processTargetService is null ||
            !string.Equals(node.NodeTypeId, "perception.capture_screen", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var nodeId = node.Id;
        if (!_captureProcessTargetDebouncers.TryGetValue(nodeId, out var debouncer))
        {
            debouncer = new Debouncer(TimeSpan.FromMilliseconds(1000));
            _captureProcessTargetDebouncers[nodeId] = debouncer;
        }

        debouncer.Debounce(() => ResolveCaptureProcessTarget(nodeId));
    }

    private void ResolveCaptureProcessTarget(Guid nodeId)
    {
        if (!_nodeVmById.TryGetValue(nodeId, out var node))
        {
            _captureProcessTargetDebouncers.Remove(nodeId);
            return;
        }

        var props = node.State.Properties ??= new JsonObject();
        var sourceMode = AutomationCaptureSourceMode.Normalize(
            AutomationNodePropertyReader.ReadString(props, AutomationNodePropertyKeys.CaptureSourceMode));
        if (!AutomationCaptureSourceMode.IsInProcessWindow(sourceMode))
            return;

        var processName = AutomationNodePropertyReader.ReadString(props, AutomationNodePropertyKeys.CaptureProcessName);
        var currentProcessId = AutomationNodePropertyReader.ReadInt(props, AutomationNodePropertyKeys.CaptureProcessId, 0);
        var resolved = AutomationProcessTargetResolution.ResolveLiveTarget(
            _processTargetService,
            processName,
            currentProcessId);
        if (resolved.ProcessId == currentProcessId)
            return;

        AutomationNodePropertyReader.WriteInt(props, AutomationNodePropertyKeys.CaptureProcessId, resolved.ProcessId);
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
            case AutomationNodeInlineEditorActionKind.BrowseOnnxModelFile:
            {
                BrowseInlineYoloOnnxModel(field);
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
            case AutomationNodeInlineEditorActionKind.PickInputModeId:
            {
                PickInputModeForNode(node);
                break;
            }
        }
    }

    [RelayCommand]
    private async Task ExecuteSecondaryInlineNodeActionAsync(AutomationInlineNodeFieldViewModel? field)
    {
        if (field is null)
            return;
        if (!_nodeVmById.TryGetValue(field.NodeId, out var node))
            return;

        switch (field.SecondaryActionKind)
        {
            case AutomationNodeInlineEditorActionKind.CaptureNeedleImageFromScreen:
            {
                await CaptureNeedleImageFromScreenForNodeAsync(node, field);
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

    private void PickInputModeForNode(AutomationCanvasNodeViewModel node)
    {
        var props = node.State.Properties ??= new JsonObject();
        var currentModeId = AutomationNodePropertyReader.ReadString(props, AutomationNodePropertyKeys.InputEmulationApiId);
        var selected = _inputModeSelectionService.PickInputModeId(Application.Current?.MainWindow, currentModeId);
        if (selected is null)
            return;

        var nextModeId = AutomationInputModeCatalog.NormalizeModeId(selected);
        var previousModeId = AutomationInputModeCatalog.NormalizeModeId(currentModeId);
        if (string.Equals(previousModeId, nextModeId, StringComparison.Ordinal))
            return;

        PushUndoCheckpoint();
        AutomationNodePropertyReader.WriteString(props, AutomationNodePropertyKeys.InputEmulationApiId, nextModeId);
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

    [RelayCommand]
    private void BrowseInlineYoloOnnxModel(AutomationInlineNodeFieldViewModel? field)
    {
        if (field is null)
            return;
        if (!string.Equals(field.NodeTypeId, "perception.find_image", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(field.PropertyKey, AutomationNodePropertyKeys.FindImageYoloOnnxPath, StringComparison.Ordinal))
        {
            return;
        }

        var userYolo = AutomationYoloOnnxPaths.GetUserModelsDirectory();
        var bundledYolo = AutomationYoloOnnxPaths.GetBundledModelsDirectory();
        var initial = Directory.Exists(userYolo) ? userYolo :
            Directory.Exists(bundledYolo) ? bundledYolo :
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var dlg = new OpenFileDialog
        {
            Filter = "ONNX model (*.onnx)|*.onnx|All files (*.*)|*.*",
            InitialDirectory = initial
        };
        if (dlg.ShowDialog() != true)
            return;

        field.TextValue = dlg.FileName;
        CommitInlineNodeField(field);
    }

    private async Task CaptureNeedleImageFromScreenForNodeAsync(
        AutomationCanvasNodeViewModel node,
        AutomationInlineNodeFieldViewModel field)
    {
        if (!string.Equals(field.NodeTypeId, "perception.find_image", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(field.PropertyKey, AutomationNodePropertyKeys.FindImageNeedlePath, StringComparison.Ordinal))
            return;

        try
        {
            var pick = await _regionPicker.PickRectanglePhysicalAsync();
            if (pick is null || pick.Rect.IsEmpty)
                return;

            PushUndoCheckpoint();
            var bmp = pick.CroppedPhysicalBitmap
                ?? _screenCaptureResolver.Resolve(null)
                    .CaptureRectanglePhysical(pick.Rect.X, pick.Rect.Y, pick.Rect.Width, pick.Rect.Height);
            var cachePath = Path.Combine(AppPaths.GetAutomationCaptureCacheDirectory(), $"needle-{node.Id:N}.png");
            SavePng(bmp, cachePath);

            var props = node.State.Properties ??= new JsonObject();
            AutomationNodePropertyReader.WriteString(props, AutomationNodePropertyKeys.FindImageNeedlePath, cachePath);
            PopulateInlineEditors(node);
            _toast.ShowInfo("AutomationWorkspace_NeedleScreenshotSavedTitle", "AutomationWorkspace_NeedleScreenshotSavedOk");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _dialogs.ShowError(ExceptionMessageFormatter.UserFacingMessage(ex),
                Local("AutomationWorkspace_NeedleScreenshotSavedTitle"));
        }
    }

    private bool CanClearCaptureRoi() =>
        SelectedNode is not null &&
        string.Equals(SelectedNode.NodeTypeId, AutomationNodeTypeIds.CaptureScreen, StringComparison.OrdinalIgnoreCase);

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
        props.Remove(AutomationNodePropertyKeys.CaptureRoiCachePath);
        AutomationNodePropertyReader.WriteString(props, AutomationNodePropertyKeys.CaptureMode, AutomationCaptureMode.Full);
        PopulateInlineEditors(node);
        if (SelectedNode?.Id == node.Id)
            RefreshRoiThumbnail(node);
    }

    public IReadOnlyList<AutomationNodeContextMenuAction> BuildNodeContextMenuActions(AutomationCanvasNodeViewModel node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return _nodeContextMenuService.BuildNodeActions(
            node.Id,
            node.NodeTypeId,
            SelectedNode?.Id,
            SelectedNode?.NodeTypeId);
    }

    public void ExecuteNodeContextMenuAction(AutomationCanvasNodeViewModel targetNode, AutomationNodeContextMenuActionKind actionKind)
    {
        ArgumentNullException.ThrowIfNull(targetNode);

        switch (actionKind)
        {
            case AutomationNodeContextMenuActionKind.CopyNodeId:
            {
                if (_nodeContextMenuService.TryCopyNodeIdToClipboard(targetNode.Id))
                    _toast.ShowInfo("AutomationNodeContextMenu_Title", "AutomationNodeContextMenu_CopyNodeId_Success");
                else
                    _toast.ShowError("AutomationNodeContextMenu_Title", "AutomationNodeContextMenu_CopyFailed");
                break;
            }
            case AutomationNodeContextMenuActionKind.CopyNodeTypeId:
            {
                if (_nodeContextMenuService.TryCopyNodeTypeIdToClipboard(targetNode.NodeTypeId))
                    _toast.ShowInfo("AutomationNodeContextMenu_Title", "AutomationNodeContextMenu_CopyNodeTypeId_Success");
                else
                    _toast.ShowError("AutomationNodeContextMenu_Title", "AutomationNodeContextMenu_CopyFailed");
                break;
            }
            case AutomationNodeContextMenuActionKind.UseAsCaptureCacheSource:
            {
                TryAssignCaptureCacheSourceFromNode(targetNode);
                break;
            }
        }
    }

    private void TryAssignCaptureCacheSourceFromNode(AutomationCanvasNodeViewModel sourceNode)
    {
        var selected = SelectedNode;
        if (selected is null)
            return;
        if (!string.Equals(selected.NodeTypeId, AutomationNodeTypeIds.CaptureScreen, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(sourceNode.NodeTypeId, AutomationNodeTypeIds.CaptureScreen, StringComparison.OrdinalIgnoreCase) ||
            selected.Id == sourceNode.Id)
        {
            _toast.ShowError("AutomationNodeContextMenu_Title", "AutomationNodeContextMenu_CaptureCacheApply_InvalidSelection");
            return;
        }

        var props = selected.State.Properties ??= new JsonObject();
        var sourceIdText = sourceNode.Id.ToString("D");
        var previous = AutomationNodePropertyReader.ReadString(props, AutomationNodePropertyKeys.CaptureCacheRefNodeId);
        if (string.Equals(previous, sourceIdText, StringComparison.OrdinalIgnoreCase))
            return;

        PushUndoCheckpoint();
        AutomationNodePropertyReader.WriteString(props, AutomationNodePropertyKeys.CaptureCacheRefNodeId, sourceIdText);
        PopulateInlineEditors(selected);
        _toast.ShowInfo("AutomationNodeContextMenu_Title", "AutomationNodeContextMenu_CaptureCacheApply_Success");
    }

    private void ClearAutomationRunLog() => AutomationRunLogLines.Clear();

    private void AppendAutomationLogLine(string line)
    {
        AutomationRunLogLines.Add(line);
        while (AutomationRunLogLines.Count > MaxAutomationRunLogDisplayedLines)
            AutomationRunLogLines.RemoveAt(0);
    }

    private async Task ExecuteScriptRunAsync(
        string startLogLine,
        string toastTitleResourceKey,
        string successToastMessageResourceKey,
        bool showFailureDialog)
    {
        if (!TryBeginAutomationRun(out var cancellationToken))
        {
            AppendAutomationLogLine(Local("AutomationWorkspace_RunAlreadyInProgressLog"));
            return;
        }

        ClearAutomationRunLog();
        AppendAutomationLogLine(startLogLine);
        try
        {
            var documentSnapshot = _serializer.Deserialize(_serializer.Serialize(_document));
            var logProgress = new Progress<string>(AppendAutomationLogLine);
            var result = await _scriptRunner.RunDocumentOnceAsync(documentSnapshot, cancellationToken, logProgress);
            AppendRunResultLogs(result);

            var message = ResolveResultMessage(result);
            if (result.Ok)
            {
                _toast.ShowInfo(toastTitleResourceKey, successToastMessageResourceKey);
                return;
            }

            if (showFailureDialog)
            {
                _dialogs.ShowWarning(
                    string.IsNullOrEmpty(message) ? Local(toastTitleResourceKey) : message,
                    Local(toastTitleResourceKey));
            }
        }
        catch (OperationCanceledException)
        {
            var result = new AutomationSmokeRunResult
            {
                Ok = false,
                MessageResourceKey = "AutomationSmoke_Cancelled"
            };
            AppendRunResultLogs(result);
            _toast.ShowInfo(toastTitleResourceKey, "AutomationWorkspace_EmergencyStopToast");
        }
        catch (Exception ex)
        {
            AppendAutomationLogLine(ExceptionMessageFormatter.UserFacingMessage(ex));
            if (showFailureDialog)
                _dialogs.ShowError(ExceptionMessageFormatter.UserFacingMessage(ex), Local(toastTitleResourceKey));
        }
        finally
        {
            EndAutomationRun();
        }
    }

    private bool TryBeginAutomationRun(out CancellationToken token)
    {
        lock (_runStateSync)
        {
            if (_activeRunCts is not null)
            {
                token = default;
                return false;
            }

            _activeRunCts = new CancellationTokenSource();
            token = _activeRunCts.Token;
        }

        IsAutomationRunInProgress = true;
        return true;
    }

    private void EndAutomationRun()
    {
        CancellationTokenSource? toDispose;
        lock (_runStateSync)
        {
            toDispose = _activeRunCts;
            _activeRunCts = null;
        }

        IsAutomationRunInProgress = false;
        toDispose?.Dispose();
    }

    private void AppendRunResultLogs(AutomationSmokeRunResult result)
    {
        var message = ResolveResultMessage(result);
        if (!string.IsNullOrEmpty(message))
            AppendAutomationLogLine(message);
        if (!string.IsNullOrEmpty(result.Detail))
            AppendAutomationLogLine(result.Detail);
    }

    private static string ResolveResultMessage(AutomationSmokeRunResult result) =>
        result.MessageResourceKey is not null ? Local(result.MessageResourceKey) : "";

    private void RefreshRoiThumbnail(AutomationCanvasNodeViewModel? node)
    {
        RoiInspectorThumbnail = null;
        RoiInspectorSummaryText = "";
        if (node?.State.Properties is null)
        {
            UpdateRoiInspectorLiveTimer();
            return;
        }

        if (!string.Equals(node.NodeTypeId, AutomationNodeTypeIds.CaptureScreen, StringComparison.OrdinalIgnoreCase))
        {
            UpdateRoiInspectorLiveTimer();
            return;
        }

        var props = node.State.Properties;
        if (!AutomationCapturePreviewSupport.TryGetPreviewableProperties(node.NodeTypeId, props, out var okProps, out var blockReason) ||
            okProps is null)
        {
            RoiInspectorSummaryText = blockReason == AutomationCapturePreviewBlockReason.CacheReference
                ? Local("AutomationRoiPreview_NoPreviewCacheRef")
                : Local("AutomationRoiPreview_NoCapture");
            UpdateRoiInspectorLiveTimer();
            return;
        }

        RoiInspectorSummaryText = AutomationCapturePreviewSupport.FormatCaptureStatus(okProps, Local);

        if (IsRoiInspectorLivePreview)
        {
            var live = _roiPreviewImageProvider.TryCaptureLivePreview(okProps);
            RoiInspectorThumbnail = live ?? _roiPreviewImageProvider.TryLoadStoredPreview(okProps);
        }
        else
            RoiInspectorThumbnail = _roiPreviewImageProvider.TryLoadStoredPreview(okProps);

        UpdateRoiInspectorLiveTimer();
    }

    private void UpdateRoiInspectorLiveTimer()
    {
        if (_roiInspectorLiveTimer is null)
        {
            _roiInspectorLiveTimer = new DispatcherTimer(
                TimeSpan.FromMilliseconds(AutomationCapturePreviewDefaults.LiveRefreshIntervalMilliseconds),
                DispatcherPriority.Normal,
                OnRoiInspectorLiveTick,
                Dispatcher.CurrentDispatcher);
        }

        var run = IsRoiInspectorLivePreview &&
            SelectedNode is not null &&
            AutomationCapturePreviewSupport.TryGetPreviewableProperties(
                SelectedNode.NodeTypeId,
                SelectedNode.State.Properties,
                out _,
                out _);

        if (run)
            _roiInspectorLiveTimer.Start();
        else
            _roiInspectorLiveTimer.Stop();
    }

    private void OnRoiInspectorLiveTick(object? sender, EventArgs e)
    {
        if (!IsRoiInspectorLivePreview || SelectedNode is null)
            return;

        if (!AutomationCapturePreviewSupport.TryGetPreviewableProperties(
                SelectedNode.NodeTypeId,
                SelectedNode.State.Properties,
                out var props,
                out _) ||
            props is null)
            return;

        var live = _roiPreviewImageProvider.TryCaptureLivePreview(props);
        if (live is not null)
            RoiInspectorThumbnail = live;
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
                var currentBool = AutomationNodePropertyReader.ReadBool(props, definition.PropertyKey);
                if (currentBool == field.BooleanValue)
                    return false;
                AutomationNodePropertyReader.WriteBool(props, definition.PropertyKey, field.BooleanValue);
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

    private static string NormalizeInlineChoice(string? raw, AutomationNodeInlineEditorDefinition definition)
    {
        if (definition.ChoiceOptions is not { Count: > 0 } options)
            return definition.DefaultTextValue.Trim();

        var trimmed = raw?.Trim() ?? "";
        foreach (var opt in options)
        {
            if (string.Equals(opt.StoredValue, trimmed, StringComparison.OrdinalIgnoreCase))
                return opt.StoredValue;
        }

        return definition.DefaultTextValue.Trim();
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
