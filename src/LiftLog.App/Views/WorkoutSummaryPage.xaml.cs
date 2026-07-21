using LiftLog.App.ViewModels;

namespace LiftLog.App.Views;

public partial class WorkoutSummaryPage : ContentPage
{
    private readonly WorkoutSummaryViewModel _viewModel;

    public WorkoutSummaryPage(WorkoutSummaryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        _viewModel.FinishCommand.Execute(null);
        return true;
    }
}
