using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Models.State;
using Gamepad_Mapping.Interfaces.Services.ControllerVisual;
using Gamepad_Mapping.ViewModels.ControllerVisual;

namespace Gamepad_Mapping.ViewModels;

public partial class VisualEditorViewModel : ObservableObject
{
    private readonly MainViewModel _mainViewModel;
    private readonly IControllerVisualService _visualService;
    private readonly IMappingsForLogicalControlQuery _mappingQuery;

    [ObservableProperty]
    private ControllerVisualViewModel _controllerVisual;

    [ObservableProperty]
    private MappingEntry? _selectedMapping;

    [ObservableProperty]
    private string? _selectedElementName;

    [ObservableProperty]
    private string? _selectedDisplayName;

    [ObservableProperty]
    private bool _isTemplateDescriptionExpanded = false;

    public string TemplateCommunityListingDescription
    {
        get => _mainViewModel.CurrentTemplateCommunityListingDescription;
        set => _mainViewModel.CurrentTemplateCommunityListingDescription = value;
    }

    public string TemplateCommunityListingDescriptionPreview
    {
        get
        {
            var normalized = (TemplateCommunityListingDescription ?? string.Empty).Trim();
            if (normalized.Length == 0)
                return string.Empty;

            const int maxLength = 120;
            return normalized.Length <= maxLength ? normalized : $"{normalized[..maxLength]}...";
        }
    }

    public bool HasTemplateCommunityListingDescription =>
        !string.IsNullOrWhiteSpace(TemplateCommunityListingDescription);

    public VisualLogicalControlMappingsViewModel LogicalControlMappings { get; }

    public bool ShowVisualCreateMappingCallout =>
        !string.IsNullOrEmpty(SelectedElementName)
        && !_mainViewModel.MappingEditorPanel.IsCreatingNewMapping
        && !HasAnyMappingForSelectedElement();

    public VisualEditorViewModel(
        MainViewModel mainViewModel,
        IControllerVisualService visualService,
        IMappingsForLogicalControlQuery mappingQuery,
        IControllerVisualLayoutSource layoutSource,
        IControllerVisualLoader controllerVisualLoader,
        IControllerVisualHighlightService highlightService)
    {
        _mainViewModel = mainViewModel;
        _visualService = visualService;
        _mappingQuery = mappingQuery;
        LogicalControlMappings = new VisualLogicalControlMappingsViewModel(
            mainViewModel,
            mappingQuery,
            visualService);

        ControllerVisual = new ControllerVisualViewModel(
            visualService,
            layoutSource,
            controllerVisualLoader,
            highlightService,
            mainViewModel.ControllerMappingOverlayLabelComposer,
            mainViewModel.ControllerVisualLayoutHelper);

        _mainViewModel.MappingEditorPanel.PropertyChanged += OnMappingEditorPanelPropertyChanged;
        _mainViewModel.MappingSelection.SelectionChanged += (_, _) =>
        {
            var mapping = _mainViewModel.MappingSelection.SelectedItem;
            SyncFromGlobalSelection(mapping);
            ControllerVisual.SelectedMapping = mapping;
        };

        ControllerVisual.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ControllerVisualViewModel.SelectedElementName))
                OnElementSelected(ControllerVisual.SelectedElementName);
        };

        _mainViewModel.Mappings.CollectionChanged += (_, _) =>
        {
            ControllerVisual.UpdateOverlay(_mainViewModel.Mappings);
            OnPropertyChanged(nameof(ShowVisualCreateMappingCallout));
        };
        ControllerVisual.UpdateOverlay(_mainViewModel.Mappings);

        _mainViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.CurrentTemplateCommunityListingDescription))
            {
                OnPropertyChanged(nameof(TemplateCommunityListingDescription));
                OnPropertyChanged(nameof(TemplateCommunityListingDescriptionPreview));
                OnPropertyChanged(nameof(HasTemplateCommunityListingDescription));
            }
        };
    }

    private bool HasAnyMappingForSelectedElement() =>
        !string.IsNullOrEmpty(SelectedElementName)
        && _mappingQuery.GetMappingsForLogicalControl(SelectedElementName!, _mainViewModel.Mappings).Count > 0;

    private void OnMappingEditorPanelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MappingEditorViewModel.IsCreatingNewMapping))
            OnPropertyChanged(nameof(ShowVisualCreateMappingCallout));
    }

    partial void OnSelectedElementNameChanged(string? value)
    {
        OnPropertyChanged(nameof(HasVisualElementSelection));
        ClearVisualSelectionCommand.NotifyCanExecuteChanged();
        LogicalControlMappings.SetElementContext(value);
        RefreshVisualSelectionState();
    }

    public bool HasVisualElementSelection => !string.IsNullOrEmpty(SelectedElementName);

    [RelayCommand(CanExecute = nameof(CanClearVisualSelection))]
    private void ClearVisualSelection() =>
        ControllerVisual.SelectedElementName = null;

    private bool CanClearVisualSelection() =>
        !string.IsNullOrEmpty(SelectedElementName);

    partial void OnSelectedMappingChanged(MappingEntry? value) =>
        OnPropertyChanged(nameof(ShowVisualCreateMappingCallout));

    private void RefreshVisualSelectionState()
    {
        OnPropertyChanged(nameof(ShowVisualCreateMappingCallout));
        _mainViewModel.RefreshRightPanelSurface();
    }

    private void SyncFromGlobalSelection(MappingEntry? mapping)
    {
        if (mapping is null)
        {
            SelectedMapping = null;
            ControllerVisual.SelectedMapping = null;
            return;
        }

        var elementId = _mappingQuery.ResolvePrimaryLogicalControlIdForMapping(mapping);
        if (string.IsNullOrEmpty(elementId))
            return;

        ControllerVisual.SelectedElementName = elementId;
        SelectedMapping = mapping;
        SelectedElementName = elementId;
        SelectedDisplayName = _visualService.GetDisplayName(elementId);
    }

    private void OnElementSelected(string? elementName)
    {
        SelectedElementName = elementName;
        SelectedDisplayName = elementName != null ? _visualService.GetDisplayName(elementName) : null;
        if (string.IsNullOrEmpty(elementName))
        {
            SelectedMapping = null;
            _mainViewModel.MappingSelection.SelectedItem = null;
            return;
        }

        var binding = _visualService.MapIdToBinding(elementName);
        if (binding is null)
        {
            SelectedMapping = null;
            _mainViewModel.MappingSelection.SelectedItem = null;
            return;
        }

        var forControl = _mappingQuery.GetMappingsForLogicalControl(elementName, _mainViewModel.Mappings);
        if (forControl.Count == 0)
        {
            SelectedMapping = null;
            _mainViewModel.MappingSelection.SelectedItem = null;
            return;
        }

        var current = _mainViewModel.MappingSelection.SelectedItem;
        if (current is not null && forControl.Any(x => ReferenceEquals(x, current)))
            return;

        var pick = forControl[0];
        SelectedMapping = pick;
        _mainViewModel.MappingSelection.SelectedItem = pick;
    }

    [RelayCommand]
    private void CreateMapping() =>
        LogicalControlMappings.AddMappingForSelectedControlCommand.Execute(null);

    [RelayCommand]
    private void ToggleTemplateDescriptionExpanded() =>
        IsTemplateDescriptionExpanded = !IsTemplateDescriptionExpanded;
}
