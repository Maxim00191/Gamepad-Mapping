using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gamepad_Mapping.Interfaces.Services;
using GamepadMapperGUI.Models;
using System.Linq;

namespace Gamepad_Mapping.ViewModels;

public partial class ControllerVisualViewModel : ObservableObject
{
    private readonly MappingEditorViewModel _mappingEditorViewModel;
    private readonly IControllerVisualService _visualService;

    [ObservableProperty]
    private string? _hoveredElementName;

    public ControllerVisualViewModel(MappingEditorViewModel mappingEditorViewModel, IControllerVisualService visualService)
    {
        _mappingEditorViewModel = mappingEditorViewModel;
        _visualService = visualService;
    }

    [RelayCommand]
    private void SelectElement(string? elementId)
    {
        if (string.IsNullOrEmpty(elementId)) return;

        var binding = _visualService.MapIdToBinding(elementId);
        if (binding == null) return;

        // Try to find existing mapping
        var existing = _mappingEditorViewModel.Mappings.FirstOrDefault(m => 
            m.From?.Type == binding.Type && m.From?.Value == binding.Value);

        if (existing != null)
        {
            _mappingEditorViewModel.SelectedMapping = existing;
        }
        else
        {
            // If no existing mapping, enter "Create New" mode with this binding pre-selected
            _mappingEditorViewModel.AddMappingCommand.Execute(null);
            _mappingEditorViewModel.InputTrigger.SyncFrom(new MappingEntry { From = binding });
        }
    }

    [RelayCommand]
    private void HoverElement(string? elementId)
    {
        HoveredElementName = string.IsNullOrEmpty(elementId) ? null : _visualService.GetDisplayName(elementId);
    }
}
