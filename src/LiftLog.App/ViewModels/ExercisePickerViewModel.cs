using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiftLog.App.Resources.Strings;
using LiftLog.Core.Services;

namespace LiftLog.App.ViewModels;

public partial class ExercisePickerViewModel(IExerciseService exerciseService) : BaseViewModel
{
    private HashSet<int> _allowedExerciseIds = [];

    [ObservableProperty]
    private ObservableCollection<ExerciseListItem> exercises = [];

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private bool isEmpty;

    public void Configure(string title, IEnumerable<int> allowedExerciseIds)
    {
        Title = title;
        _allowedExerciseIds = allowedExerciseIds.ToHashSet();
        SearchText = string.Empty;
        Exercises = [];
        IsEmpty = false;
        ErrorMessage = null;
        HasLoaded = false;
    }

    public Task LoadAsync() => RunBusyAsync(async () =>
    {
        var matchingExercises = await exerciseService.GetAllAsync(SearchText);
        Exercises = new ObservableCollection<ExerciseListItem>(
            matchingExercises
                .Where(exercise => _allowedExerciseIds.Contains(exercise.Id))
                .Select(exercise => new ExerciseListItem(exercise)));
        IsEmpty = Exercises.Count == 0;
    }, AppText.FailedToLoadExercises);

    [RelayCommand]
    private Task SearchAsync() => LoadAsync();

    [RelayCommand]
    private Task RetryAsync() => LoadAsync();
}
