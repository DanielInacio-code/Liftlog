using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiftLog.App.Services;
using LiftLog.App.Resources.Strings;
using LiftLog.Core.Services;

namespace LiftLog.App.ViewModels;

public partial class RoutinesViewModel : BaseViewModel
{
    private readonly IRoutineService _routineService;
    private readonly IWorkoutService _workoutService;
    private readonly INavigationService _navigationService;

    public RoutinesViewModel(
        IRoutineService routineService,
        IWorkoutService workoutService,
        INavigationService navigationService)
    {
        _routineService = routineService;
        _workoutService = workoutService;
        _navigationService = navigationService;
        Title = Resources.Strings.AppText.RoutinesTitle;
    }

    [ObservableProperty]
    private ObservableCollection<RoutineListItem> routines = [];

    [ObservableProperty]
    private bool isEmpty;

    public Task LoadAsync() => RunBusyAsync(async () =>
    {
        var routines = await _routineService.GetAllAsync();

        Routines = new ObservableCollection<RoutineListItem>(
            routines.Select(routine => new RoutineListItem(routine)));

        IsEmpty = Routines.Count == 0;
    }, AppText.FailedToLoadRoutines);

    [RelayCommand]
    private Task CreateRoutineAsync() =>
        _navigationService.GoToAsync(NavigationRoutes.RoutineEdit);

    [RelayCommand]
    private Task OpenExercisesAsync() =>
        _navigationService.GoToAsync(NavigationRoutes.Exercises);

    [RelayCommand]
    private Task OpenHistoryAsync() =>
        _navigationService.GoToAsync(NavigationRoutes.History);

    [RelayCommand]
    private Task RetryAsync() => LoadAsync();

    [RelayCommand]
    private Task EditRoutineAsync(RoutineListItem routine) =>
        _navigationService.GoToAsync(
            NavigationRoutes.RoutineEdit,
            new Dictionary<string, object> { ["RoutineId"] = routine.Id });

    [RelayCommand]
    private Task StartRoutineAsync(RoutineListItem routine) => RunBusyAsync(async () =>
    {
        if (await _workoutService.HasActiveAsync())
        {
            ErrorMessage = AppText.WorkoutAlreadyInProgress;
            return;
        }

        await _navigationService.GoToAsync(
            NavigationRoutes.ActiveWorkout,
            new Dictionary<string, object> { ["RoutineId"] = routine.Id });
    }, AppText.FailedToCheckActiveWorkout);

    [RelayCommand]
    private async Task DeleteRoutineAsync(RoutineListItem routine)
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = null;
            await _routineService.DeleteAsync(routine.Id);
            Routines.Remove(routine);
            IsEmpty = Routines.Count == 0;
        }
        catch (RoutineValidationException exception)
        {
            ErrorMessage = exception.Message;
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(exception);
            ErrorMessage = AppText.FailedToDeleteRoutine;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
