using LiftLog.App.ViewModels;

namespace LiftLog.App.Views;

public partial class ProfilePage : ContentPage
{
    private readonly ProfileViewModel _viewModel;

    public ProfilePage(ProfileViewModel viewModel)
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

    protected override bool OnBackButtonPressed()
    {
        if (PeriodPickerOverlay.IsVisible)
        {
            HidePeriodPicker();
            return true;
        }

        return base.OnBackButtonPressed();
    }

    private void OnPeriodPickerClicked(object? sender, EventArgs eventArgs)
    {
        UpdatePeriodButtons();
        PeriodPickerOverlay.IsVisible = true;
    }

    private void OnPeriodPickerBackdropTapped(object? sender, TappedEventArgs eventArgs) =>
        HidePeriodPicker();

    private void OnPeriodPickerCloseClicked(object? sender, EventArgs eventArgs) =>
        HidePeriodPicker();

    private void OnPeriodOptionClicked(object? sender, EventArgs eventArgs)
    {
        if (sender is not Button { CommandParameter: string periodName } ||
            !Enum.TryParse<ProfilePeriod>(periodName, out var period))
        {
            return;
        }

        _viewModel.SelectedPeriod = _viewModel.PeriodOptions.Single(option => option.Period == period);
        _viewModel.RefreshPeriod();
        HidePeriodPicker();
    }

    private void UpdatePeriodButtons()
    {
        UpdatePeriodButton(LastMonthButton, ProfilePeriod.LastMonth);
        UpdatePeriodButton(ThreeMonthsButton, ProfilePeriod.ThreeMonths);
        UpdatePeriodButton(OneYearButton, ProfilePeriod.OneYear);
    }

    private void UpdatePeriodButton(Button button, ProfilePeriod period)
    {
        var selected = _viewModel.SelectedPeriod?.Period == period;
        button.BackgroundColor = selected
            ? GetColor("Accent")
            : GetColor("SurfaceDark");
        button.TextColor = selected
            ? GetColor("Black")
            : GetColor("TextPrimaryDark");
        button.BorderColor = selected
            ? GetColor("Accent")
            : GetColor("OutlineDark");
    }

    private static Color GetColor(string key) =>
        (Color)(Application.Current?.Resources[key]
            ?? throw new InvalidOperationException($"The {key} colour resource was not found."));

    private void HidePeriodPicker() => PeriodPickerOverlay.IsVisible = false;
}
