using LiftLog.App.Resources.Strings;
using LiftLog.App.Services;
using LiftLog.App.ViewModels;

namespace LiftLog.App.Views;

public partial class RoutinesPage : ContentPage, IQueryAttributable
{
    private readonly RoutinesViewModel _viewModel;
    private readonly ExercisesViewModel _exercisesViewModel;
    private readonly HistoryViewModel _historyViewModel;
    private readonly WorkoutHubState _workoutHubState;
    private bool _isAppeared;
    private RoutineListItem? _routinePendingDeletion;

    public RoutinesPage(
        RoutinesViewModel viewModel,
        ExercisesViewModel exercisesViewModel,
        HistoryViewModel historyViewModel,
        WorkoutHubState workoutHubState)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        ExercisesSection.BindingContext = _exercisesViewModel = exercisesViewModel;
        HistorySection.BindingContext = _historyViewModel = historyViewModel;
        _workoutHubState = workoutHubState;
        UpdateSectionVisualState();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _isAppeared = true;
        UpdateSectionVisualState();
        await Task.WhenAll(
            LoadCurrentSectionAsync(),
            ActiveWorkoutBanner.EnsureCurrentAsync());
    }

    protected override void OnDisappearing()
    {
        _isAppeared = false;
        base.OnDisappearing();
    }

    protected override bool OnBackButtonPressed()
    {
        if (DeleteRoutineOverlay.IsVisible)
        {
            HideDeleteRoutineDialog();
            return true;
        }

        return base.OnBackButtonPressed();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (!query.TryGetValue("section", out var value))
        {
            return;
        }

        var section = value?.ToString()?.ToLowerInvariant() switch
        {
            "routines" => WorkoutHubSection.Routines,
            "exercises" => WorkoutHubSection.Exercises,
            "history" => WorkoutHubSection.History,
            _ => (WorkoutHubSection?)null
        };

        if (section is null)
        {
            return;
        }

        _workoutHubState.CurrentSection = section.Value;

        UpdateSectionVisualState();

        if (_isAppeared)
        {
            Dispatcher.Dispatch(() => _ = LoadCurrentSectionAsync());
        }
    }

    private async void OnRoutinesSectionClicked(object? sender, EventArgs eventArgs) =>
        await SelectSectionAsync(WorkoutHubSection.Routines);

    private async void OnExercisesSectionClicked(object? sender, EventArgs eventArgs) =>
        await SelectSectionAsync(WorkoutHubSection.Exercises);

    private async void OnHistorySectionClicked(object? sender, EventArgs eventArgs) =>
        await SelectSectionAsync(WorkoutHubSection.History);

    private async Task SelectSectionAsync(WorkoutHubSection section)
    {
        _workoutHubState.CurrentSection = section;
        UpdateSectionVisualState();
        await LoadCurrentSectionAsync();
    }

    private Task LoadCurrentSectionAsync() => _workoutHubState.CurrentSection switch
    {
        WorkoutHubSection.Exercises => _exercisesViewModel.LoadAsync(),
        WorkoutHubSection.History => _historyViewModel.LoadAsync(),
        _ => _viewModel.LoadAsync()
    };

    private void UpdateSectionVisualState()
    {
        var currentSection = _workoutHubState.CurrentSection;
        RoutinesSection.IsVisible = currentSection == WorkoutHubSection.Routines;
        ExercisesSection.IsVisible = currentSection == WorkoutHubSection.Exercises;
        HistorySection.IsVisible = currentSection == WorkoutHubSection.History;

        SetSectionButtonStyle(RoutinesSectionButton, currentSection == WorkoutHubSection.Routines);
        SetSectionButtonStyle(ExercisesSectionButton, currentSection == WorkoutHubSection.Exercises);
        SetSectionButtonStyle(HistorySectionButton, currentSection == WorkoutHubSection.History);
    }

    private void SetSectionButtonStyle(Button button, bool isSelected)
    {
        var resourceKey = isSelected
            ? "WorkoutSectionButtonSelectedStyle"
            : "WorkoutSectionButtonStyle";

        button.Style = (Style)Resources[resourceKey];
    }

    private void OnDeleteClicked(object? sender, EventArgs eventArgs)
    {
        if (sender is not Button { BindingContext: RoutineListItem routine })
        {
            return;
        }

        _routinePendingDeletion = routine;
        DeleteRoutineMessageLabel.Text = AppText.DeleteRoutineConfirmation;
        DeleteRoutineOverlay.IsVisible = true;
    }

    private void OnDeleteRoutineBackdropTapped(object? sender, TappedEventArgs eventArgs) =>
        HideDeleteRoutineDialog();

    private void OnKeepRoutineClicked(object? sender, EventArgs eventArgs) =>
        HideDeleteRoutineDialog();

    private async void OnConfirmDeleteRoutineClicked(object? sender, EventArgs eventArgs)
    {
        var routine = _routinePendingDeletion;
        HideDeleteRoutineDialog();

        if (routine is not null)
        {
            await _viewModel.DeleteRoutineCommand.ExecuteAsync(routine);
        }
    }

    private void HideDeleteRoutineDialog()
    {
        DeleteRoutineOverlay.IsVisible = false;
        _routinePendingDeletion = null;
    }

    private async void OnExerciseSelected(object? sender, SelectionChangedEventArgs eventArgs)
    {
        if (eventArgs.CurrentSelection.FirstOrDefault() is not ExerciseListItem exercise)
        {
            return;
        }

        ExercisesCollection.SelectedItem = null;
        await _exercisesViewModel.OpenExerciseCommand.ExecuteAsync(exercise);
    }

    private async void OnWorkoutSelected(object? sender, SelectionChangedEventArgs eventArgs)
    {
        if (eventArgs.CurrentSelection.FirstOrDefault() is not HistoryWorkoutListItem workout)
        {
            return;
        }

        HistoryCollection.SelectedItem = null;
        await _historyViewModel.OpenWorkoutCommand.ExecuteAsync(workout);
    }

}
