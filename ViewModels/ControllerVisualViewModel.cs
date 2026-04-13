using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gamepad_Mapping.Interfaces.Services;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Models.ControllerVisual;

namespace Gamepad_Mapping.ViewModels;

public partial class ControllerVisualViewModel : ObservableObject
{
    private readonly IControllerVisualService _visualService;
    private readonly IControllerVisualLayoutSource _layoutSource;

    [ObservableProperty]
    private string? _hoveredElementName;

    [ObservableProperty]
    private string? _selectedElementName;

    [ObservableProperty]
    private MappingEntry? _selectedMapping;

    [ObservableProperty]
    private ControllerVisualLayoutDescriptor _activeLayout;

    public IControllerVisualLoader Loader { get; }

    public ControllerVisualViewModel(
        IControllerVisualService visualService,
        IControllerVisualLayoutSource layoutSource,
        IControllerVisualLoader loader)
    {
        _visualService = visualService;
        _layoutSource = layoutSource;
        Loader = loader;
        _activeLayout = layoutSource.GetActiveLayout();
    }

    public void RefreshActiveLayout()
    {
        ActiveLayout = _layoutSource.GetActiveLayout();
    }

    [RelayCommand]
    private void SelectElement(string? elementId)
    {
        SelectedElementName = elementId;
    }

    [RelayCommand]
    private void HoverElement(string? elementId)
    {
        HoveredElementName = string.IsNullOrEmpty(elementId) ? null : _visualService.GetDisplayName(elementId);
    }
}
