using LiftLog.App.ViewModels;

namespace LiftLog.App.Views;

public partial class HistoryPage : ContentPage
{
    private readonly HistoryViewModel _viewModel;

    public HistoryPage(HistoryViewModel viewModel)
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

    private async void OnWorkoutSelected(object? sender, SelectionChangedEventArgs eventArgs)
    {
        if (eventArgs.CurrentSelection.FirstOrDefault() is not HistoryWorkoutListItem workout)
        {
            return;
        }

        HistoryCollection.SelectedItem = null;
        await _viewModel.OpenWorkoutCommand.ExecuteAsync(workout);
    }
}
