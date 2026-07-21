using LiftLog.App.ViewModels;

namespace LiftLog.App.Views;

public partial class ExerciseEditPage : ContentPage
{
    private readonly ExerciseEditViewModel _viewModel;

    public ExerciseEditPage(ExerciseEditViewModel viewModel)
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
