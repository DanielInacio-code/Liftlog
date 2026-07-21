using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiftLog.App.Resources.Strings;
using LiftLog.Core.Services;

namespace LiftLog.App.ViewModels;

public partial class DataBackupViewModel(
    IHistoryService historyService,
    IRoutineService routineService,
    IExerciseService exerciseService,
    IBodyMeasurementService measurementService) : BaseViewModel
{
    [ObservableProperty]
    private string workoutCount = "0";

    [ObservableProperty]
    private string routineCount = "0";

    [ObservableProperty]
    private string exerciseCount = "0";

    [ObservableProperty]
    private string measurementCount = "0";

    public Task LoadAsync() => RunBusyAsync(async () =>
    {
        var workoutsTask = historyService.GetCompletedAsync();
        var routinesTask = routineService.GetAllAsync();
        var exercisesTask = exerciseService.GetAllAsync();
        var measurementsTask = measurementService.GetAllAsync();
        await Task.WhenAll(workoutsTask, routinesTask, exercisesTask, measurementsTask);

        WorkoutCount = (await workoutsTask).Count.ToString();
        RoutineCount = (await routinesTask).Count.ToString();
        ExerciseCount = (await exercisesTask).Count.ToString();
        MeasurementCount = (await measurementsTask).Count.ToString();
    }, AppText.FailedToLoadLocalData);

    [RelayCommand]
    private Task RetryAsync() => LoadAsync();
}
