using System.Globalization;
using LiftLog.App.Resources.Strings;
using LiftLog.App.Services;
using LiftLog.App.ViewModels;
using LiftLog.Core.Models;

namespace LiftLog.App.Views;

public partial class ActiveWorkoutPage : ContentPage, IQueryAttributable
{
    private readonly ActiveWorkoutViewModel _viewModel;
    private IDispatcherTimer? _durationTimer;
    private bool _canSaveChanges;
    private WorkoutSetItem? _selectedWorkoutSet;
    private double? _pendingRpe;
    private ActiveWorkoutExerciseItem? _selectedExercise;
    private int _pendingRestTimerSeconds;
    private int? _routineIdToStart;
    private CancellationTokenSource? _initialLoadCancellation;

    public ActiveWorkoutPage(ActiveWorkoutViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _viewModel.RestTimerCompleted += OnRestTimerCompleted;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.IsPreparing = true;
        _initialLoadCancellation?.Cancel();
        _initialLoadCancellation?.Dispose();
        _initialLoadCancellation = new CancellationTokenSource();
        _ = LoadAfterTransitionAsync(_initialLoadCancellation.Token);
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("RoutineId", out var value) &&
            int.TryParse(value?.ToString(), out var routineId))
        {
            _routineIdToStart = routineId;
        }
    }

    protected override void OnDisappearing()
    {
        _initialLoadCancellation?.Cancel();
        _viewModel.IsPreparing = false;
        _canSaveChanges = false;
        _durationTimer?.Stop();
        base.OnDisappearing();
    }

    private async Task LoadAfterTransitionAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Yield only long enough for the page shell and preparation overlay
            // to render; the grouped list no longer needs a fixed warm-up delay.
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();

            if (_routineIdToStart is { } routineId)
            {
                _routineIdToStart = null;
                await _viewModel.StartAndLoadAsync(routineId);
            }
            else
            {
                await _viewModel.LoadAsync();
            }

            cancellationToken.ThrowIfCancellationRequested();
            _canSaveChanges = true;
            _durationTimer ??= CreateDurationTimer();
            _durationTimer.Start();
            _viewModel.RefreshRestTimer();
        }
        catch (OperationCanceledException)
        {
            // Leaving the page during the transition cancels its deferred load.
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                _viewModel.IsPreparing = false;
            }
        }
    }

    protected override bool OnBackButtonPressed()
    {
        if (FinishWorkoutOverlay.IsVisible)
        {
            HideFinishWorkoutDialog();
            return true;
        }

        if (CancelWorkoutOverlay.IsVisible)
        {
            HideCancelWorkoutDialog();
            return true;
        }

        if (BottomSheetOverlay.IsVisible)
        {
            HideBottomSheet();
            return true;
        }

        return base.OnBackButtonPressed();
    }

    private IDispatcherTimer CreateDurationTimer()
    {
        var timer = Dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(1);
        timer.Tick += (_, _) =>
        {
            _viewModel.RefreshDuration();
            _viewModel.RefreshRestTimer();
        };
        return timer;
    }

    private void OnRestTimerCompleted(object? sender, EventArgs eventArgs)
    {
        try
        {
            HapticFeedback.Default.Perform(HapticFeedbackType.LongPress);
        }
        catch (FeatureNotSupportedException)
        {
            // The visual timer still completes normally on devices without haptics.
        }

        SemanticScreenReader.Default.Announce(AppText.RestComplete);
    }

    private async void OnSetFieldUnfocused(object? sender, FocusEventArgs eventArgs)
    {
        if (_canSaveChanges && sender is Entry { BindingContext: WorkoutSetItem workoutSet })
        {
            await _viewModel.SaveSetCommand.ExecuteAsync(workoutSet);
        }
    }

    private async void OnExerciseNotesUnfocused(object? sender, FocusEventArgs eventArgs)
    {
        if (_canSaveChanges && sender is Entry { BindingContext: ActiveWorkoutExerciseItem exercise })
        {
            await _viewModel.SaveExerciseNotesCommand.ExecuteAsync(exercise);
        }
    }

    private void OnSetTypeClicked(object? sender, EventArgs eventArgs)
    {
        if (_canSaveChanges && sender is Button { BindingContext: WorkoutSetItem workoutSet })
        {
            _selectedWorkoutSet = workoutSet;
            ShowBottomSheet("Select set type", BottomSheetSection.SetType);
        }
    }

    private void OnRpeClicked(object? sender, EventArgs eventArgs)
    {
        if (_canSaveChanges && sender is Button { BindingContext: WorkoutSetItem workoutSet })
        {
            _selectedWorkoutSet = workoutSet;
            _pendingRpe = workoutSet.Rpe;
            UpdateSelectedRpeLabel();
            ShowBottomSheet("Select RPE", BottomSheetSection.Rpe);
        }
    }

    private void OnRestTimerClicked(object? sender, TappedEventArgs eventArgs)
    {
        if (!_canSaveChanges || eventArgs.Parameter is not ActiveWorkoutExerciseItem exercise)
        {
            return;
        }

        _selectedExercise = exercise;
        _pendingRestTimerSeconds = exercise.RestTimerSeconds;
        RestTimerExerciseNameLabel.Text = exercise.Name;
        UpdateSelectedRestTimerLabel();
        ShowBottomSheet(AppText.SetRestTimer, BottomSheetSection.RestTimer);
    }

    private void OnRestTimerAdjustClicked(object? sender, EventArgs eventArgs)
    {
        if (sender is not Button { CommandParameter: string adjustmentText } ||
            !int.TryParse(adjustmentText, CultureInfo.InvariantCulture, out var adjustment))
        {
            return;
        }

        _pendingRestTimerSeconds = Math.Clamp(
            _pendingRestTimerSeconds + adjustment,
            0,
            600);
        UpdateSelectedRestTimerLabel();
    }

    private void OnRestTimerPresetClicked(object? sender, EventArgs eventArgs)
    {
        if (sender is not Button { CommandParameter: string secondsText } ||
            !int.TryParse(secondsText, CultureInfo.InvariantCulture, out var seconds))
        {
            return;
        }

        _pendingRestTimerSeconds = seconds;
        UpdateSelectedRestTimerLabel();
    }

    private async void OnRestTimerDoneClicked(object? sender, EventArgs eventArgs)
    {
        if (_selectedExercise is null)
        {
            return;
        }

        await _viewModel.SetRestTimerAsync(_selectedExercise, _pendingRestTimerSeconds);
        HideBottomSheet();
    }

    private void UpdateSelectedRestTimerLabel() =>
        SelectedRestTimerLabel.Text = ActiveWorkoutExerciseItem.FormatRestDuration(
            _pendingRestTimerSeconds);

    private async void OnSetTypeOptionClicked(object? sender, EventArgs eventArgs)
    {
        if (_selectedWorkoutSet is null || sender is not Button { CommandParameter: string action })
        {
            return;
        }

        if (action == "Remove")
        {
            await _viewModel.DeleteSetCommand.ExecuteAsync(_selectedWorkoutSet);
        }
        else if (Enum.TryParse<TrainingSetType>(action, out var setType))
        {
            _selectedWorkoutSet.SetType = setType;
            await _viewModel.SaveSetCommand.ExecuteAsync(_selectedWorkoutSet);
        }

        HideBottomSheet();
    }

    private void OnRpeOptionClicked(object? sender, EventArgs eventArgs)
    {
        if (_selectedWorkoutSet is null ||
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

    private async void OnRpeDoneClicked(object? sender, EventArgs eventArgs)
    {
        if (_selectedWorkoutSet is null)
        {
            return;
        }

        _selectedWorkoutSet.Rpe = _pendingRpe;
        await _viewModel.SaveSetCommand.ExecuteAsync(_selectedWorkoutSet);
        HideBottomSheet();
    }

    private void UpdateSelectedRpeLabel() =>
        SelectedRpeValueLabel.Text = _pendingRpe?.ToString("0.#", CultureInfo.InvariantCulture) ?? "0";

    private async void OnAddExerciseClicked(object? sender, EventArgs eventArgs)
    {
        if (!_canSaveChanges ||
            !await _viewModel.EnsureAvailableExercisesLoadedAsync() ||
            !_viewModel.HasAvailableExercises)
        {
            return;
        }

        await OpenExercisePickerAsync("Add exercise", async exerciseId =>
        {
            var exercise = _viewModel.AvailableExercises.FirstOrDefault(item => item.Id == exerciseId);
            if (exercise is not null)
            {
                await _viewModel.AddExerciseCommand.ExecuteAsync(exercise);
            }
        });
    }

    private void OnExerciseMenuClicked(object? sender, EventArgs eventArgs)
    {
        if (_canSaveChanges && sender is Button { BindingContext: ActiveWorkoutExerciseItem exercise })
        {
            _selectedExercise = exercise;
            ShowBottomSheet(exercise.Name, BottomSheetSection.Exercise);
        }
    }

    private async void OnExerciseOptionClicked(object? sender, EventArgs eventArgs)
    {
        if (_selectedExercise is null || sender is not Button { CommandParameter: string action })
        {
            return;
        }

        switch (action)
        {
            case "MoveUp":
                await _viewModel.MoveExerciseUpCommand.ExecuteAsync(_selectedExercise);
                break;
            case "MoveDown":
                await _viewModel.MoveExerciseDownCommand.ExecuteAsync(_selectedExercise);
                break;
            case "Replace":
                if (!await _viewModel.EnsureAvailableExercisesLoadedAsync())
                {
                    return;
                }

                var exerciseToReplace = _selectedExercise;
                HideBottomSheet();
                await OpenExercisePickerAsync("Replace exercise", async exerciseId =>
                {
                    var replacement = _viewModel.AvailableExercises
                        .FirstOrDefault(item => item.Id == exerciseId);
                    if (replacement is not null)
                    {
                        await _viewModel.ReplaceExerciseAsync(exerciseToReplace, replacement);
                    }
                });
                return;
            case "Remove":
                await _viewModel.RemoveExerciseCommand.ExecuteAsync(_selectedExercise);
                break;
        }

        HideBottomSheet();
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
        RestTimerSheetContent.IsVisible = section == BottomSheetSection.RestTimer;
        ExerciseSheetContent.IsVisible = section == BottomSheetSection.Exercise;
        BottomSheetOverlay.IsVisible = true;
    }

    private void HideBottomSheet()
    {
        BottomSheetOverlay.IsVisible = false;
        SetTypeSheetContent.IsVisible = false;
        RpeSheetContent.IsVisible = false;
        RestTimerSheetContent.IsVisible = false;
        ExerciseSheetContent.IsVisible = false;
        _selectedWorkoutSet = null;
        _pendingRpe = null;
        _pendingRestTimerSeconds = 0;
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

    private async void OnMinimizeClicked(object? sender, EventArgs eventArgs)
    {
        if (BottomSheetOverlay.IsVisible)
        {
            HideBottomSheet();
            return;
        }

        await Shell.Current.GoToAsync("..");
    }

    private void OnFinishClicked(object? sender, EventArgs eventArgs)
    {
        DismissKeyboard();

        if (BottomSheetOverlay.IsVisible)
        {
            HideBottomSheet();
        }

        FinishWorkoutMessageLabel.Text = _viewModel.IncompleteSetCount > 0
            ? string.Format(AppText.FinishWorkoutWithIncompleteSets, _viewModel.IncompleteSetCount)
            : AppText.FinishWorkoutConfirmation;
        FinishWorkoutOverlay.IsVisible = true;
    }

    private void OnFinishWorkoutBackdropTapped(object? sender, TappedEventArgs eventArgs) =>
        HideFinishWorkoutDialog();

    private void OnKeepWorkoutOpenClicked(object? sender, EventArgs eventArgs) =>
        HideFinishWorkoutDialog();

    private async void OnConfirmFinishWorkoutClicked(object? sender, EventArgs eventArgs)
    {
        HideFinishWorkoutDialog();
        await _viewModel.FinishWorkoutCommand.ExecuteAsync(null);
    }

    private void HideFinishWorkoutDialog() =>
        FinishWorkoutOverlay.IsVisible = false;

    private void OnCancelClicked(object? sender, EventArgs eventArgs)
    {
        DismissKeyboard();

        if (BottomSheetOverlay.IsVisible)
        {
            HideBottomSheet();
        }

        CancelWorkoutOverlay.IsVisible = true;
    }

    private void OnCancelWorkoutBackdropTapped(object? sender, TappedEventArgs eventArgs) =>
        HideCancelWorkoutDialog();

    private void OnKeepTrainingClicked(object? sender, EventArgs eventArgs) =>
        HideCancelWorkoutDialog();

    private async void OnConfirmCancelWorkoutClicked(object? sender, EventArgs eventArgs)
    {
        HideCancelWorkoutDialog();
        await _viewModel.CancelWorkoutCommand.ExecuteAsync(null);
    }

    private void HideCancelWorkoutDialog() =>
        CancelWorkoutOverlay.IsVisible = false;

    private enum BottomSheetSection
    {
        SetType,
        Rpe,
        RestTimer,
        Exercise
    }
}
