using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using GamepadMapperGUI.Models;
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

    public ObservableCollection<ProcessInfo> RecentProcesses => _mainViewModel.RecentProcesses;

    public ProcessInfo? SelectedTargetProcess
    {
        get => _mainViewModel.SelectedTargetProcess;
        set => _mainViewModel.SelectedTargetProcess = value;
    }

    public bool IsProcessTargetingEnabled => _mainViewModel.IsProcessTargetingEnabled;

    public string TargetStatusText => _mainViewModel.TargetStatusText;

    public AppTargetingState TargetState => _mainViewModel.TargetState;

    public ICommand RefreshProcessesCommand => _mainViewModel.RefreshProcessesCommand;

    public ICommand ClearTargetProcessCommand => _mainViewModel.ClearTargetProcessCommand;

    private void MainViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.RecentProcesses):
                OnPropertyChanged(nameof(RecentProcesses));
                break;
            case nameof(MainViewModel.SelectedTargetProcess):
                OnPropertyChanged(nameof(SelectedTargetProcess));
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
