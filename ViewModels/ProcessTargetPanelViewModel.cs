using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using GamepadMapperGUI.Services;

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
        }
    }
}
