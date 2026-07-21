using LiftLog.App.Resources.Strings;
using LiftLog.App.ViewModels;

namespace LiftLog.App.Views;

public partial class ExerciseDetailsPage : ContentPage
{
    private readonly ExerciseDetailsViewModel _viewModel;

    public ExerciseDetailsPage(ExerciseDetailsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }

    private async void OnDeleteClicked(object? sender, EventArgs eventArgs)
    {
        var confirmed = await DisplayAlertAsync(
            AppText.DeleteExercise,
            AppText.DeleteExerciseConfirmation,
            AppText.Delete,
            AppText.Cancel);

        if (confirmed)
        {
            await _viewModel.DeleteCommand.ExecuteAsync(null);
        }
    }
}
