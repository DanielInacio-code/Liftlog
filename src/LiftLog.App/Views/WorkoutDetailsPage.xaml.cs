using LiftLog.App.ViewModels;

namespace LiftLog.App.Views;

public partial class WorkoutDetailsPage : ContentPage
{
    private readonly WorkoutDetailsViewModel _viewModel;

    public WorkoutDetailsPage(WorkoutDetailsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }
}
