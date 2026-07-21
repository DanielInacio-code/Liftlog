using LiftLog.App.ViewModels;

namespace LiftLog.App.Views;

public partial class DataBackupPage : ContentPage
{
    private readonly DataBackupViewModel _viewModel;

    public DataBackupPage(DataBackupViewModel viewModel)
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
