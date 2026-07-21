using LiftLog.App.ViewModels;

namespace LiftLog.App.Views;

public partial class ExercisesPage : ContentPage
{
    private readonly ExercisesViewModel _viewModel;

    public ExercisesPage(ExercisesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await Task.WhenAll(
            _viewModel.LoadAsync(),
            ActiveWorkoutBanner.EnsureCurrentAsync());
    }

    private async void OnExerciseSelected(object? sender, SelectionChangedEventArgs eventArgs)
    {
        if (eventArgs.CurrentSelection.FirstOrDefault() is not ExerciseListItem exercise)
        {
            return;
        }

        ExercisesCollection.SelectedItem = null;
        await _viewModel.OpenExerciseCommand.ExecuteAsync(exercise);
    }
}
