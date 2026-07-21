using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiftLog.App.Services;
using LiftLog.App.Resources.Strings;
using LiftLog.Core.Models;
using LiftLog.Core.Services;

namespace LiftLog.App.ViewModels;

public partial class ExercisesViewModel : BaseViewModel
{
    private readonly IExerciseService _exerciseService;
    private readonly INavigationService _navigationService;

    public ExercisesViewModel(
        IExerciseService exerciseService,
        INavigationService navigationService)
    {
        _exerciseService = exerciseService;
        _navigationService = navigationService;
        Title = Resources.Strings.AppText.ExercisesTitle;
    }

    [ObservableProperty]
    private ObservableCollection<ExerciseListItem> exercises = [];

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private bool isEmpty;

    public Task LoadAsync() => RunBusyAsync(async () =>
    {
        var exercises = await _exerciseService.GetAllAsync(SearchText);

        Exercises = new ObservableCollection<ExerciseListItem>(
            exercises.Select(exercise => new ExerciseListItem(exercise)));

        IsEmpty = Exercises.Count == 0;
    }, AppText.FailedToLoadExercises);

    [RelayCommand]
    private Task SearchAsync() => LoadAsync();

    [RelayCommand]
    private Task RetryAsync() => LoadAsync();

    [RelayCommand]
    private Task CreateExerciseAsync() =>
        _navigationService.GoToAsync(NavigationRoutes.ExerciseEdit);

    [RelayCommand]
    private Task OpenExerciseAsync(ExerciseListItem exercise) =>
        _navigationService.GoToAsync(
            NavigationRoutes.ExerciseDetails,
            new Dictionary<string, object> { ["ExerciseId"] = exercise.Id });
}
