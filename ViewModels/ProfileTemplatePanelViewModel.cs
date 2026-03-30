using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Gamepad_Mapping.ViewModels;

public class ProfileTemplatePanelViewModel : ObservableObject
{
    private readonly MainViewModel _mainViewModel;

    public ProfileTemplatePanelViewModel(MainViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
        _mainViewModel.PropertyChanged += MainViewModelOnPropertyChanged;
    }

    public ObservableCollection<TemplateOption> AvailableTemplates => _mainViewModel.AvailableTemplates;

    public TemplateOption? SelectedTemplate
    {
        get => _mainViewModel.SelectedTemplate;
        set => _mainViewModel.SelectedTemplate = value;
    }

    public string CurrentTemplateDisplayName
    {
        get => _mainViewModel.CurrentTemplateDisplayName;
        set => _mainViewModel.CurrentTemplateDisplayName = value;
    }

    public int MappingCount => _mainViewModel.MappingCount;

    public string NewProfileGameId
    {
        get => _mainViewModel.NewProfileGameId;
        set => _mainViewModel.NewProfileGameId = value;
    }

    public string NewProfileDisplayName
    {
        get => _mainViewModel.NewProfileDisplayName;
        set => _mainViewModel.NewProfileDisplayName = value;
    }

    public ICommand SaveProfileCommand => _mainViewModel.SaveProfileCommand;

    public ICommand DeleteSelectedProfileCommand => _mainViewModel.DeleteSelectedProfileCommand;

    public ICommand CreateProfileCommand => _mainViewModel.CreateProfileCommand;

    public ICommand ReloadTemplateCommand => _mainViewModel.ReloadTemplateCommand;

    private void MainViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.AvailableTemplates):
                OnPropertyChanged(nameof(AvailableTemplates));
                break;
            case nameof(MainViewModel.SelectedTemplate):
                OnPropertyChanged(nameof(SelectedTemplate));
                break;
            case nameof(MainViewModel.CurrentTemplateDisplayName):
                OnPropertyChanged(nameof(CurrentTemplateDisplayName));
                break;
            case nameof(MainViewModel.MappingCount):
                OnPropertyChanged(nameof(MappingCount));
                break;
            case nameof(MainViewModel.NewProfileGameId):
                OnPropertyChanged(nameof(NewProfileGameId));
                break;
            case nameof(MainViewModel.NewProfileDisplayName):
                OnPropertyChanged(nameof(NewProfileDisplayName));
                break;
        }
    }
}
