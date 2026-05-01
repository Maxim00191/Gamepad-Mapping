#nullable enable

using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gamepad_Mapping.Interfaces.Services.ControllerVisual;
using Gamepad_Mapping.Models.Core.Visual;
using Gamepad_Mapping.Utils.ControllerVisual;
using GamepadMapperGUI.Core;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Services.Infrastructure;

namespace Gamepad_Mapping.ViewModels;

public partial class VisualLogicalControlMappingsViewModel : ObservableObject
{
    private readonly MainViewModel _mainViewModel;
    private readonly IMappingsForLogicalControlQuery _query;
    private readonly IControllerVisualService _visualService;
    private readonly TranslationService? _translationService;
    private string? _elementId;
    private string? _controlDisplayName;
    private bool _suppressSelectionSync;

    public ObservableCollection<LogicalControlMappingListItem> Items { get; } = [];

    [ObservableProperty]
    private LogicalControlMappingListItem? _selectedItem;

    public bool HasElementContext => !string.IsNullOrEmpty(_elementId);

    public bool ShowSelectControlHint => !HasElementContext;

    public bool ShowUnmappedHint => HasElementContext && Items.Count == 0;

    public bool ShowMappingList => HasElementContext && Items.Count > 0;

    public string? ControlDisplayName => _controlDisplayName;

    public string MappingsCountSummary =>
        _translationService is null
            ? Items.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : LogicalControlMappingsCountPhrases.FormatMappingCount(Items.Count, _translationService);

    public VisualLogicalControlMappingsViewModel(
        MainViewModel mainViewModel,
        IMappingsForLogicalControlQuery query,
        IControllerVisualService visualService)
    {
        _mainViewModel = mainViewModel;
        _query = query;
        _visualService = visualService;

        if (AppUiLocalization.TryTranslationService() is { } loc)
        {
            _translationService = loc;
            loc.PropertyChanged += OnTranslationServicePropertyChanged;
        }

        _mainViewModel.MappingSelection.SelectionChanged += OnVisualMappingSelectionChanged;
        _mainViewModel.Mappings.CollectionChanged += OnMappingsCollectionChanged;
    }

    private void OnTranslationServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TranslationService.Culture) or "Item[]")
        {
            OnPropertyChanged(nameof(MappingsCountSummary));
            RebuildItems();
        }
    }

    public void SetElementContext(string? elementId)
    {
        _elementId = elementId;
        _controlDisplayName = string.IsNullOrEmpty(elementId) ? null : _visualService.GetDisplayName(elementId);
        OnPropertyChanged(nameof(HasElementContext));
        OnPropertyChanged(nameof(ShowSelectControlHint));
        OnPropertyChanged(nameof(ControlDisplayName));
        RebuildItems();
    }

    partial void OnSelectedItemChanged(LogicalControlMappingListItem? value)
    {
        if (_suppressSelectionSync || value is null)
            return;

        _mainViewModel.MappingSelection.SelectedItem = value.Mapping;
    }

    [RelayCommand]
    public void AddMappingForSelectedControl()
    {
        if (string.IsNullOrEmpty(_elementId))
            return;

        var binding = _visualService.MapIdToBinding(_elementId);
        if (binding is null)
            return;

        if (_mainViewModel.IsPlayStationGamepadActive &&
            _visualService.IsTouchpadSurfaceLogicalControl(_elementId))
            binding = GamepadTouchpadFromValueCatalog.CreateDefaultSurfaceBinding();

        _mainViewModel.MappingsWorkspace.History.ExecuteTransaction(() =>
        {
            _mainViewModel.MappingEditorPanel.AddMappingCommand.Execute(null);
            _mainViewModel.MappingEditorPanel.InputTrigger.SyncFrom(new MappingEntry { From = binding });
        });
        _mainViewModel.RefreshRightPanelSurface();
        _mainViewModel.RequestFocusMappingDetailsFirstField();
    }

    private void OnVisualMappingSelectionChanged(object? sender, EventArgs e)
    {
        ApplySelectionFromVisualSelection();
    }

    private void OnMappingsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        RebuildItems();

    private void RebuildItems()
    {
        Items.Clear();
        if (string.IsNullOrEmpty(_elementId))
        {
            ApplySelectionFromVisualSelection();
            return;
        }

        foreach (var m in _query.GetMappingsForLogicalControl(_elementId, _mainViewModel.Mappings))
        {
            Items.Add(new LogicalControlMappingListItem
            {
                Mapping = m,
                ActionSummaryLine = m.OutputSummaryForControllerOverlay ?? string.Empty,
                InputSummaryLine = ControllerMappingFromDisplayFormatter.FormatInputLine(_visualService, m)
            });
        }

        OnPropertyChanged(nameof(ShowUnmappedHint));
        OnPropertyChanged(nameof(ShowMappingList));
        OnPropertyChanged(nameof(MappingsCountSummary));
        ApplySelectionFromVisualSelection();
    }

    private void ApplySelectionFromVisualSelection()
    {
        var sel = _mainViewModel.MappingSelection.SelectedItem;
        _suppressSelectionSync = true;
        try
        {
            SelectedItem = sel is null
                ? null
                : Items.FirstOrDefault(i => ReferenceEquals(i.Mapping, sel));
        }
        finally
        {
            _suppressSelectionSync = false;
        }
    }
}
