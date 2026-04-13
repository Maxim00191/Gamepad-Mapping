using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gamepad_Mapping.Interfaces.Services;
using GamepadMapperGUI.Models;
using System.Linq;
using System.Collections.Generic;

namespace Gamepad_Mapping.ViewModels;

public partial class ControllerVisualViewModel : ObservableObject
{
    private readonly IControllerVisualService _visualService;

    [ObservableProperty]
    private string? _hoveredElementName;

    [ObservableProperty]
    private string? _selectedElementName;

    [ObservableProperty]
    private MappingEntry? _selectedMapping;

    public ControllerVisualViewModel(IControllerVisualService visualService)
    {
        _visualService = visualService;
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
