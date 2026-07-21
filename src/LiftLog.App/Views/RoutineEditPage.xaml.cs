using System.Globalization;
using LiftLog.App.Services;
using LiftLog.App.ViewModels;
using LiftLog.Core.Models;

namespace LiftLog.App.Views;

public partial class RoutineEditPage : ContentPage
{
    private readonly RoutineEditViewModel _viewModel;
    private RoutineSetItem? _selectedRoutineSet;
    private double? _pendingRpe;
    private RoutineExerciseItem? _selectedExercise;

    public RoutineEditPage(RoutineEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }

    private async void OnBackClicked(object? sender, EventArgs eventArgs)
    {
        if (BottomSheetOverlay.IsVisible)
        {
            HideBottomSheet();
            return;
        }

        await Shell.Current.GoToAsync("..");
    }

    private void OnSetTypeClicked(object? sender, EventArgs eventArgs)
    {
        if (sender is not Button { BindingContext: RoutineSetItem routineSet })
        {
            return;
        }

        _selectedRoutineSet = routineSet;
        ShowBottomSheet("Select set type", BottomSheetSection.SetType);
    }

    private void OnRpeClicked(object? sender, EventArgs eventArgs)
    {
        if (sender is not Button { BindingContext: RoutineSetItem routineSet })
        {
            return;
        }

        _selectedRoutineSet = routineSet;
        _pendingRpe = routineSet.Rpe;
        UpdateSelectedRpeLabel();
        ShowBottomSheet("Select RPE", BottomSheetSection.Rpe);
    }

    private void OnExerciseMenuClicked(object? sender, EventArgs eventArgs)
    {
        if (sender is not Button { BindingContext: RoutineExerciseItem exercise })
        {
            return;
        }

        _selectedExercise = exercise;
        ShowBottomSheet(exercise.Name, BottomSheetSection.Exercise);
    }

    private async void OnAddExerciseClicked(object? sender, EventArgs eventArgs)
    {
        await OpenExercisePickerAsync("Add exercise", exerciseId =>
        {
            var exercise = _viewModel.AvailableExercises.FirstOrDefault(item => item.Id == exerciseId);
            if (exercise is not null)
            {
                _viewModel.ExerciseToAdd = exercise;
                _viewModel.AddExerciseCommand.Execute(null);
            }

            return Task.CompletedTask;
        });
    }

    private void OnSetTypeOptionClicked(object? sender, EventArgs eventArgs)
    {
        if (_selectedRoutineSet is null || sender is not Button { CommandParameter: string action })
        {
            return;
        }

        if (action == "Remove")
        {
            _viewModel.DeleteSetCommand.Execute(_selectedRoutineSet);
        }
        else if (Enum.TryParse<TrainingSetType>(action, out var setType))
        {
            _selectedRoutineSet.SetType = setType;
        }

        HideBottomSheet();
    }

    private void OnRpeOptionClicked(object? sender, EventArgs eventArgs)
    {
        if (_selectedRoutineSet is null ||
            sender is not Button { CommandParameter: string action } ||
            !double.TryParse(action, NumberStyles.Number, CultureInfo.InvariantCulture, out var rpe))
        {
            return;
        }

        _pendingRpe = rpe;
        UpdateSelectedRpeLabel();
    }

    private void OnRpeClearClicked(object? sender, EventArgs eventArgs)
    {
        _pendingRpe = null;
        UpdateSelectedRpeLabel();
    }

    private void OnRpeDoneClicked(object? sender, EventArgs eventArgs)
    {
        if (_selectedRoutineSet is null)
        {
            return;
        }

        _selectedRoutineSet.Rpe = _pendingRpe;
        HideBottomSheet();
    }

    private void UpdateSelectedRpeLabel() =>
        SelectedRpeValueLabel.Text = _pendingRpe?.ToString("0.#", CultureInfo.InvariantCulture) ?? "0";

    private async void OnExerciseOptionClicked(object? sender, EventArgs eventArgs)
    {
        if (_selectedExercise is null || sender is not Button { CommandParameter: string action })
        {
            return;
        }

        switch (action)
        {
            case "MoveUp":
                _viewModel.MoveExerciseUpCommand.Execute(_selectedExercise);
                HideBottomSheet();
                break;
            case "MoveDown":
                _viewModel.MoveExerciseDownCommand.Execute(_selectedExercise);
                HideBottomSheet();
                break;
            case "Replace":
                var exerciseToReplace = _selectedExercise;
                HideBottomSheet();
                await OpenExercisePickerAsync("Replace exercise", exerciseId =>
                {
                    var replacement = _viewModel.AvailableExercises
                        .FirstOrDefault(item => item.Id == exerciseId);
                    if (replacement is not null)
                    {
                        _viewModel.ReplaceExercise(exerciseToReplace, replacement);
                    }

                    return Task.CompletedTask;
                });
                return;
            case "Remove":
                _viewModel.RemoveExerciseCommand.Execute(_selectedExercise);
                HideBottomSheet();
                break;
        }
    }

    private Task OpenExercisePickerAsync(string title, Func<int, Task> selectionCallback)
    {
        var parameters = new Dictionary<string, object>
        {
            [ExercisePickerPage.TitleParameter] = title,
            [ExercisePickerPage.AllowedExerciseIdsParameter] =
                _viewModel.AvailableExercises.Select(item => item.Id).ToArray(),
            [ExercisePickerPage.SelectionCallbackParameter] = selectionCallback
        };

        return Shell.Current.GoToAsync(
            NavigationRoutes.ExercisePicker,
            true,
            new ShellNavigationQueryParameters(parameters));
    }

    private void OnBottomSheetCloseClicked(object? sender, EventArgs eventArgs) =>
        HideBottomSheet();

    private void OnBottomSheetBackdropTapped(object? sender, TappedEventArgs eventArgs) =>
        HideBottomSheet();

    private void ShowBottomSheet(string title, BottomSheetSection section)
    {
        DismissKeyboard();
        BottomSheetTitle.Text = title;
        SetTypeSheetContent.IsVisible = section == BottomSheetSection.SetType;
        RpeSheetContent.IsVisible = section == BottomSheetSection.Rpe;
        ExerciseSheetContent.IsVisible = section == BottomSheetSection.Exercise;
        BottomSheetOverlay.IsVisible = true;
    }

    private void HideBottomSheet()
    {
        BottomSheetOverlay.IsVisible = false;
        SetTypeSheetContent.IsVisible = false;
        RpeSheetContent.IsVisible = false;
        ExerciseSheetContent.IsVisible = false;
        _selectedRoutineSet = null;
        _pendingRpe = null;
        _selectedExercise = null;
    }

    private void DismissKeyboard()
    {
#if ANDROID
        var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
        var currentFocus = activity?.CurrentFocus;
        var windowToken = currentFocus?.WindowToken ?? activity?.Window?.DecorView?.WindowToken;
#endif

        this.GetVisualTreeDescendants()
            .OfType<Entry>()
            .FirstOrDefault(entry => entry.IsFocused)
            ?.Unfocus();

#if ANDROID
        currentFocus?.ClearFocus();
        if (windowToken is not null &&
            activity?.GetSystemService(Android.Content.Context.InputMethodService) is
                Android.Views.InputMethods.InputMethodManager inputMethodManager)
        {
            inputMethodManager.HideSoftInputFromWindow(
                windowToken,
                Android.Views.InputMethods.HideSoftInputFlags.None);
        }
#endif
    }

    private enum BottomSheetSection
    {
        SetType,
        Rpe,
        Exercise
    }
}
