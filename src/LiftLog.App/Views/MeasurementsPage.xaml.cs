using LiftLog.App.ViewModels;

namespace LiftLog.App.Views;

public partial class MeasurementsPage : ContentPage
{
    private readonly MeasurementsViewModel _viewModel;

    public MeasurementsPage(MeasurementsViewModel viewModel)
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
