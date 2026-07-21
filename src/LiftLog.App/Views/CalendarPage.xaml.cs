using LiftLog.App.ViewModels;

namespace LiftLog.App.Views;

public partial class CalendarPage : ContentPage
{
    private readonly CalendarViewModel _viewModel;

    public CalendarPage(CalendarViewModel viewModel)
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
