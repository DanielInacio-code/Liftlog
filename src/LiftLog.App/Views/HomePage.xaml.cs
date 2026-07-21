using LiftLog.App.ViewModels;

namespace LiftLog.App.Views;

public partial class HomePage : ContentPage
{
    private readonly HomeViewModel _viewModel;

    public HomePage(HomeViewModel viewModel)
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
}
