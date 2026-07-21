using LiftLog.App.ViewModels;

namespace LiftLog.App.Views;

public partial class ProgressPage : ContentPage
{
    private readonly ProgressViewModel _viewModel;
    private bool _loaded;

    public ProgressPage(ProgressViewModel viewModel)
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
        _loaded = true;
    }

    private async void OnExerciseChanged(object? sender, EventArgs eventArgs)
    {
        if (_loaded)
        {
            await _viewModel.LoadSelectedAsync();
        }
    }
}
