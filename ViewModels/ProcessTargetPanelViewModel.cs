using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamepadMapperGUI.Models.State;
using GamepadMapperGUI.Services.Infrastructure;

namespace Gamepad_Mapping.ViewModels;

public class ProcessTargetPanelViewModel : ObservableObject
{
    private readonly MainViewModel _mainViewModel;

    public ProcessTargetPanelViewModel(MainViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
        _mainViewModel.PropertyChanged += MainViewModelOnPropertyChanged;
    }

    public string TemplateTargetProcessName
    {
        get => _mainViewModel.TemplateTargetProcessName;
        set => _mainViewModel.TemplateTargetProcessName = value;
    }

    public bool IsProcessTargetingEnabled => _mainViewModel.IsProcessTargetingEnabled;

    public string TargetStatusText => _mainViewModel.TargetStatusText;

    public AppTargetingState TargetState => _mainViewModel.TargetState;

    public IRelayCommand RefreshDeclaredProcessTargetCommand => _mainViewModel.RefreshDeclaredProcessTargetCommand;

    /// <summary>Soft-warning highlight when the declared name has not resolved to a live PID yet.</summary>
    public bool ShouldHighlightTargetProcessRefresh => _mainViewModel.SelectedTargetProcess?.ProcessId == 0;

    public string TargetProcessRefreshToolTip =>
        ShouldHighlightTargetProcessRefresh
            ? AppUiLocalization.GetString("TargetProcessRefresh_AttentionTooltip")
            : AppUiLocalization.GetString("TargetProcessRefreshTooltip");

    private void MainViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.TemplateTargetProcessName):
                OnPropertyChanged(nameof(TemplateTargetProcessName));
                break;
            case nameof(MainViewModel.IsProcessTargetingEnabled):
                OnPropertyChanged(nameof(IsProcessTargetingEnabled));
                break;
            case nameof(MainViewModel.TargetStatusText):
                OnPropertyChanged(nameof(TargetStatusText));
                break;
            case nameof(MainViewModel.TargetState):
                OnPropertyChanged(nameof(TargetState));
                break;
            case nameof(MainViewModel.SelectedTargetProcess):
                OnPropertyChanged(nameof(ShouldHighlightTargetProcessRefresh));
                OnPropertyChanged(nameof(TargetProcessRefreshToolTip));
                break;
        }
    }
}
