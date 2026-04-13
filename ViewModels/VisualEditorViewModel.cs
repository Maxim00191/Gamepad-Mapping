using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamepadMapperGUI.Models;
using Gamepad_Mapping.Interfaces.Services;

namespace Gamepad_Mapping.ViewModels;

public partial class VisualEditorViewModel : ObservableObject
{
    private readonly MainViewModel _mainViewModel;
    private readonly IControllerVisualService _visualService;

    [ObservableProperty]
    private ControllerVisualViewModel _controllerVisual;

    [ObservableProperty]
    private MappingEntry? _selectedMapping;

    [ObservableProperty]
    private string? _selectedElementName;

    [ObservableProperty]
    private string? _selectedDisplayName;

    public bool ShowVisualCreateMappingCallout =>
        !string.IsNullOrEmpty(SelectedElementName)
        && SelectedMapping is null
        && !_mainViewModel.MappingEditorPanel.IsCreatingNewMapping;

    public VisualEditorViewModel(
        MainViewModel mainViewModel,
        IControllerVisualService visualService,
        IControllerVisualLayoutSource layoutSource,
        IControllerVisualLoader controllerVisualLoader)
    {
        _mainViewModel = mainViewModel;
        _visualService = visualService;
        ControllerVisual = new ControllerVisualViewModel(visualService, layoutSource, controllerVisualLoader);

        _mainViewModel.MappingEditorPanel.PropertyChanged += OnMappingEditorPanelPropertyChanged;

        ControllerVisual.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ControllerVisualViewModel.SelectedElementName))
                OnElementSelected(ControllerVisual.SelectedElementName);
        };

        _mainViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.SelectedMapping))
            {
                SyncFromGlobalSelection(_mainViewModel.SelectedMapping);
                ControllerVisual.SelectedMapping = _mainViewModel.SelectedMapping;
            }
        };
    }

    private void OnMappingEditorPanelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MappingEditorViewModel.IsCreatingNewMapping))
            OnPropertyChanged(nameof(ShowVisualCreateMappingCallout));
    }

    partial void OnSelectedElementNameChanged(string? value) =>
        OnPropertyChanged(nameof(ShowVisualCreateMappingCallout));

    partial void OnSelectedMappingChanged(MappingEntry? value) =>
        OnPropertyChanged(nameof(ShowVisualCreateMappingCallout));

    private void SyncFromGlobalSelection(MappingEntry? mapping)
    {
        if (mapping == null) return;

        foreach (var elementId in _visualService.EnumerateMappedLogicalControlIds())
        {
            var binding = _visualService.MapIdToBinding(elementId);
            if (binding != null && mapping.From?.Type == binding.Type && mapping.From?.Value == binding.Value)
            {
                ControllerVisual.SelectedElementName = elementId;
                SelectedMapping = mapping;
                SelectedElementName = elementId;
                SelectedDisplayName = _visualService.GetDisplayName(elementId);
                break;
            }
        }
    }

    private void OnElementSelected(string? elementName)
    {
        SelectedElementName = elementName;
        SelectedDisplayName = elementName != null ? _visualService.GetDisplayName(elementName) : null;
        if (string.IsNullOrEmpty(elementName))
        {
            SelectedMapping = null;
            _mainViewModel.SelectedMapping = null;
            return;
        }

        var binding = _visualService.MapIdToBinding(elementName);
        if (binding == null)
        {
            SelectedMapping = null;
            _mainViewModel.SelectedMapping = null;
            return;
        }

        var existing = _mainViewModel.Mappings.FirstOrDefault(m =>
            m.From?.Type == binding.Type &&
            m.From?.Value == binding.Value);

        if (existing != null)
        {
            SelectedMapping = existing;
            _mainViewModel.SelectedMapping = existing;
        }
        else
        {
            SelectedMapping = null;
            _mainViewModel.SelectedMapping = null;
        }
    }

    [RelayCommand]
    private void CreateMapping()
    {
        if (string.IsNullOrEmpty(SelectedElementName)) return;

        var binding = _visualService.MapIdToBinding(SelectedElementName);
        if (binding == null) return;

        _mainViewModel.MappingEditorPanel.AddMappingCommand.Execute(null);
        _mainViewModel.MappingEditorPanel.InputTrigger.SyncFrom(new MappingEntry { From = binding });

        SelectedMapping = _mainViewModel.SelectedMapping;
    }
}
