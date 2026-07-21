using CommunityToolkit.Mvvm.Input;
using LiftLog.App.Services;

namespace LiftLog.App.ViewModels;

public partial class SettingsViewModel(INavigationService navigationService) : BaseViewModel
{
    [RelayCommand]
    private Task OpenDataBackupAsync() =>
        navigationService.GoToAsync(NavigationRoutes.DataBackup);
}
