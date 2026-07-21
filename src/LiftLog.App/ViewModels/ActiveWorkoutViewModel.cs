using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiftLog.App.Services;
using LiftLog.App.Resources.Strings;
using LiftLog.Core.Models;
using LiftLog.Core.Services;

namespace LiftLog.App.ViewModels;

public partial class ActiveWorkoutViewModel(
    IWorkoutService workoutService,
    IExerciseService exerciseService,
    IStatisticsService statisticsService,
    INavigationService navigationService) : BaseViewModel
{
    private WorkoutSession? _session;
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly List<Exercise> _allExercises = [];
    private bool _availableExercisesLoaded;
    private DateTimeOffset? _restTimerEndsAt;
    private int? _restTimerExerciseId;
    private int? _routineIdPendingStart;
    private int? _completedWorkoutSessionId;
    private bool _cancelledWorkoutCommitted;

    public event EventHandler? RestTimerCompleted;

    [ObservableProperty]
    private ObservableCollection<ActiveWorkoutExerciseItem> exercises = [];

    [ObservableProperty]
    private IReadOnlyList<Exercise> availableExercises = [];

    [ObservableProperty]
    private string routineName = string.Empty;

    [ObservableProperty]
    private string startedAtText = string.Empty;

    [ObservableProperty]
    private string durationText = "00:00";

    [ObservableProperty]
    private int incompleteSetCount;

    [ObservableProperty]
    private string volumeText = WorkoutDisplayFormatter.FormatVolume(0);

    [ObservableProperty]
    private string completedSetsText = "0";

    [ObservableProperty]
    private bool hasAvailableExercises;

    [ObservableProperty]
    private bool isRestTimerRunning;

    [ObservableProperty]
    private string restTimerExerciseName = string.Empty;

    [ObservableProperty]
    private string restTimerRemainingText = "00:00";

    [ObservableProperty]
    private bool isPreparing;

    public Task LoadAsync() => RunBusyAsync(() => LoadWorkoutCoreAsync(), AppText.FailedToLoadWorkout);

    public Task StartAndLoadAsync(int routineId)
    {
        _routineIdPendingStart = routineId;
        return RunBusyAsync(async () =>
        {
            var session = await workoutService.StartAsync(routineId);
            _routineIdPendingStart = null;
            await LoadWorkoutCoreAsync(session);
        }, AppText.FailedToStartWorkout);
    }

    private async Task LoadWorkoutCoreAsync(WorkoutSession? session = null)
    {
            _session = session ?? await workoutService.GetActiveAsync()
                ?? throw new WorkoutValidationException("There is no workout in progress.");
            _completedWorkoutSessionId = null;
            _cancelledWorkoutCommitted = false;

            RoutineName = _session.RoutineName;
            StartedAtText = _session.StartedAt.ToLocalTime().ToString("HH:mm");
            _allExercises.Clear();
            _availableExercisesLoaded = false;
            AvailableExercises = [];
            HasAvailableExercises = false;

            var exerciseIds = _session.Exercises
                .Where(exercise => exercise.ExerciseId.HasValue)
                .Select(exercise => exercise.ExerciseId!.Value)
                .Distinct()
                .ToArray();
            var previousPerformancesTask = workoutService.GetPreviousPerformancesAsync(
                exerciseIds,
                _session.Id);
            var hasCompletedSets = _session.Exercises
                .SelectMany(exercise => exercise.Sets)
                .Any(workoutSet => workoutSet.IsCompleted);
            var recordSetIdsTask = hasCompletedSets
                ? statisticsService.GetWeightPersonalRecordSetIdsAsync(_session.Id)
                : Task.FromResult<IReadOnlySet<int>>(new HashSet<int>());

            await Task.WhenAll(previousPerformancesTask, recordSetIdsTask);
            var previousPerformances = await previousPerformancesTask;

            var exerciseItems = new List<ActiveWorkoutExerciseItem>(_session.Exercises.Count);
            foreach (var exercise in _session.Exercises.OrderBy(item => item.Position))
            {
                PreviousExercisePerformance? previous = null;
                if (exercise.ExerciseId is { } exerciseId)
                {
                    previousPerformances.TryGetValue(exerciseId, out previous);
                }

                exerciseItems.Add(CreateExerciseItem(exercise, previous));
            }

            var recordSetIds = await recordSetIdsTask;
            foreach (var workoutSet in exerciseItems.SelectMany(exercise => exercise.Sets))
            {
                workoutSet.IsPersonalRecord = recordSetIds.Contains(workoutSet.Id);
            }

            Exercises = new ObservableCollection<ActiveWorkoutExerciseItem>(exerciseItems);

            RefreshDuration();
            RefreshWorkoutStats();
    }

    public async Task<bool> EnsureAvailableExercisesLoadedAsync()
    {
        if (_availableExercisesLoaded)
        {
            RefreshAvailableExercises();
            return true;
        }

        await RunBusyAsync(async () =>
        {
            var exercises = await exerciseService.GetAllAsync();
            _allExercises.Clear();
            _allExercises.AddRange(exercises);
            _availableExercisesLoaded = true;
            RefreshAvailableExercises();
        }, AppText.FailedToLoadExercises);

        return _availableExercisesLoaded;
    }

    public void RefreshDuration()
    {
        if (_session is null)
        {
            return;
        }

        var duration = workoutService.CalculateDuration(_session);
        DurationText = duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}"
            : $"{duration.Minutes:00}:{duration.Seconds:00}";
    }

    public void RefreshRestTimer(DateTimeOffset? now = null)
    {
        if (!IsRestTimerRunning || _restTimerEndsAt is null)
        {
            return;
        }

        var remaining = (int)Math.Ceiling((_restTimerEndsAt.Value - (now ?? DateTimeOffset.UtcNow)).TotalSeconds);
        if (remaining <= 0)
        {
            StopRestTimer();
            RestTimerCompleted?.Invoke(this, EventArgs.Empty);
            return;
        }

        RestTimerRemainingText = FormatCountdown(remaining);
    }

    public Task SetRestTimerAsync(ActiveWorkoutExerciseItem exercise, int seconds) =>
        RunWorkoutOperationAsync(async () =>
        {
            var saved = await workoutService.SetRestTimerAsync(exercise.Id, seconds);
            exercise.RestTimerSeconds = saved.RestTimerSeconds;
            if (seconds == 0 && _restTimerExerciseId == exercise.Id)
            {
                StopRestTimer();
            }
        }, AppText.FailedToSetRestTimer);

    [RelayCommand]
    private Task SaveExerciseNotesAsync(ActiveWorkoutExerciseItem exercise) =>
        RunWorkoutOperationAsync(async () =>
        {
            var saved = await workoutService.UpdateExerciseNotesAsync(exercise.Id, exercise.Notes);
            exercise.Notes = saved.Notes ?? string.Empty;
        }, AppText.FailedToSaveExerciseNotes);

    [RelayCommand]
    private Task RetryAsync()
    {
        if (_completedWorkoutSessionId is not null)
        {
            return FinishWorkoutAsync();
        }

        if (_cancelledWorkoutCommitted)
        {
            return CancelWorkoutAsync();
        }

        return _routineIdPendingStart is { } routineId
            ? StartAndLoadAsync(routineId)
            : LoadAsync();
    }

    [RelayCommand]
    private async Task AddSetAsync(ActiveWorkoutExerciseItem exercise)
    {
        await RunWorkoutOperationAsync(async () =>
        {
            var added = await workoutService.AddSetAsync(exercise.Id);
            exercise.Sets.Add(new WorkoutSetItem(added));
            exercise.RefreshPreviousPerformance();
            RefreshWorkoutStats();
        }, AppText.FailedToAddSet);
    }

    [RelayCommand]
    private Task AddExerciseAsync(Exercise exercise) =>
        RunWorkoutOperationAsync(async () =>
        {
            if (_session is null)
            {
                return;
            }

            var added = await workoutService.AddExerciseAsync(_session.Id, exercise.Id);
            Exercises.Add(await CreateExerciseItemAsync(added));
            RefreshAvailableExercises();
            RefreshWorkoutStats();
        }, AppText.FailedToAddExercise);

    [RelayCommand]
    private Task RemoveExerciseAsync(ActiveWorkoutExerciseItem exercise) =>
        RunWorkoutOperationAsync(async () =>
        {
            await workoutService.RemoveExerciseAsync(exercise.Id);
            Exercises.Remove(exercise);
            RefreshAvailableExercises();
            RefreshWorkoutStats();
        }, AppText.FailedToRemoveExercise);

    [RelayCommand]
    private Task MoveExerciseUpAsync(ActiveWorkoutExerciseItem exercise) =>
        MoveExerciseAsync(exercise, -1);

    [RelayCommand]
    private Task MoveExerciseDownAsync(ActiveWorkoutExerciseItem exercise) =>
        MoveExerciseAsync(exercise, 1);

    public Task ReplaceExerciseAsync(
        ActiveWorkoutExerciseItem current,
        Exercise replacement) =>
        RunWorkoutOperationAsync(async () =>
        {
            var index = Exercises.IndexOf(current);
            if (index < 0)
            {
                return;
            }

            var replaced = await workoutService.ReplaceExerciseAsync(current.Id, replacement.Id);
            var replacementItem = await CreateExerciseItemAsync(replaced);
            Exercises[index] = replacementItem;
            RefreshAvailableExercises();
            RefreshWorkoutStats();
            await TryRefreshPersonalRecordsAsync(replacementItem);
        }, AppText.FailedToChangeExercise);

    [RelayCommand]
    private Task SaveSetAsync(WorkoutSetItem workoutSet) =>
        RunWorkoutOperationAsync(async () =>
        {
            var input = ParseSetInput(workoutSet);
            if (workoutSet.MatchesSavedInput(input))
            {
                return;
            }

            await workoutService.UpdateSetAsync(workoutSet.Id, input);
            workoutSet.MarkSaved(input);
            RefreshWorkoutStats();
            await TryRefreshPersonalRecordsForSetAsync(workoutSet);
        }, AppText.FailedToSaveSet);

    [RelayCommand]
    private Task ToggleSetCompletionAsync(WorkoutSetItem workoutSet) =>
        RunWorkoutOperationAsync(async () =>
        {
            var wasCompleted = workoutSet.IsCompleted;
            var input = ParseSetInput(workoutSet);
            var saved = await workoutService.UpdateSetAndCompletionAsync(
                workoutSet.Id,
                input,
                !workoutSet.IsCompleted);
            workoutSet.MarkSaved(input);
            workoutSet.IsCompleted = saved.IsCompleted;
            RefreshWorkoutStats();

            if (!wasCompleted && saved.IsCompleted)
            {
                var exercise = Exercises.FirstOrDefault(item => item.Sets.Contains(workoutSet));
                if (exercise is { RestTimerSeconds: > 0 })
                {
                    StartRestTimer(exercise);
                }
            }

            await TryRefreshPersonalRecordsForSetAsync(workoutSet);
        }, AppText.FailedToUpdateSet);

    [RelayCommand]
    private void AddRestTime()
    {
        if (!IsRestTimerRunning || _restTimerEndsAt is null)
        {
            return;
        }

        _restTimerEndsAt = _restTimerEndsAt.Value.AddSeconds(30);
        RefreshRestTimer();
    }

    [RelayCommand]
    private void StopRestTimer()
    {
        IsRestTimerRunning = false;
        _restTimerEndsAt = null;
        _restTimerExerciseId = null;
        RestTimerExerciseName = string.Empty;
        RestTimerRemainingText = "00:00";
    }

    [RelayCommand]
    private Task DeleteSetAsync(WorkoutSetItem workoutSet) =>
        RunWorkoutOperationAsync(async () =>
        {
            var exercise = Exercises.Single(item => item.Sets.Contains(workoutSet));
            await workoutService.DeleteSetAsync(workoutSet.Id);

            exercise.Sets.Remove(workoutSet);
            for (var index = 0; index < exercise.Sets.Count; index++)
            {
                exercise.Sets[index].SetNumber = index + 1;
            }

            exercise.RefreshPreviousPerformance();
            RefreshWorkoutStats();
            await TryRefreshPersonalRecordsAsync(exercise);
        }, AppText.FailedToDeleteSet);

    [RelayCommand]
    private async Task FinishWorkoutAsync()
    {
        if (_session is null && _completedWorkoutSessionId is null)
        {
            return;
        }

        await RunWorkoutOperationAsync(async () =>
        {
            StopRestTimer();
            if (_completedWorkoutSessionId is null)
            {
                var completed = await workoutService.CompleteAsync(_session!.Id);
                _completedWorkoutSessionId = completed.Id;
            }

            await NavigateAfterCompletedWorkoutAsync(_completedWorkoutSessionId.Value);
        }, AppText.FailedToFinishWorkout);
    }

    [RelayCommand]
    private async Task CancelWorkoutAsync()
    {
        if (_session is null && !_cancelledWorkoutCommitted)
        {
            return;
        }

        await RunWorkoutOperationAsync(async () =>
        {
            StopRestTimer();
            if (!_cancelledWorkoutCommitted)
            {
                await workoutService.CancelAsync(_session!.Id);
                _cancelledWorkoutCommitted = true;
            }

            await navigationService.GoToAsync(NavigationRoutes.Home);
        }, AppText.FailedToCancelWorkout);
    }

    private async Task NavigateAfterCompletedWorkoutAsync(int workoutSessionId)
    {
        try
        {
            await navigationService.GoToAsync(
                NavigationRoutes.WorkoutSummary,
                new Dictionary<string, object> { ["WorkoutSessionId"] = workoutSessionId });
        }
        catch (Exception summaryNavigationException)
        {
            System.Diagnostics.Debug.WriteLine(summaryNavigationException);
            await navigationService.GoToAsync(NavigationRoutes.Home);
        }
    }

    private async Task RunWorkoutOperationAsync(Func<Task> operation, string friendlyError)
    {
        await _operationGate.WaitAsync();

        try
        {
            IsBusy = true;
            ErrorMessage = null;
            await operation();
        }
        catch (WorkoutValidationException exception)
        {
            ErrorMessage = exception.Message;
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(exception);
            ErrorMessage = friendlyError;
        }
        finally
        {
            IsBusy = false;
            _operationGate.Release();
        }
    }

    private Task MoveExerciseAsync(ActiveWorkoutExerciseItem exercise, int offset) =>
        RunWorkoutOperationAsync(async () =>
        {
            var currentIndex = Exercises.IndexOf(exercise);
            var newIndex = currentIndex + offset;
            if (currentIndex < 0 || newIndex < 0 || newIndex >= Exercises.Count)
            {
                return;
            }

            await workoutService.MoveExerciseAsync(exercise.Id, newIndex);
            Exercises.Move(currentIndex, newIndex);
        }, AppText.FailedToMoveExercise);

    private async Task<ActiveWorkoutExerciseItem> CreateExerciseItemAsync(WorkoutExercise exercise)
    {
        var item = new ActiveWorkoutExerciseItem(exercise);
        if (_session is null || exercise.ExerciseId is null)
        {
            return item;
        }

        var previous = await workoutService.GetPreviousPerformanceAsync(
            exercise.ExerciseId.Value,
            _session.Id);
        return CreateExerciseItem(exercise, previous);
    }

    private static ActiveWorkoutExerciseItem CreateExerciseItem(
        WorkoutExercise exercise,
        PreviousExercisePerformance? previous)
    {
        var item = new ActiveWorkoutExerciseItem(exercise);
        item.PreviousPerformance = FormatPreviousPerformance(previous);
        item.ApplyPreviousPerformance(previous);
        return item;
    }

    private void RefreshAvailableExercises()
    {
        if (!_availableExercisesLoaded)
        {
            return;
        }

        var selectedIds = Exercises
            .Where(item => item.ExerciseId.HasValue)
            .Select(item => item.ExerciseId!.Value)
            .ToHashSet();
        AvailableExercises = _allExercises
            .Where(item => !selectedIds.Contains(item.Id))
            .ToArray();

        HasAvailableExercises = AvailableExercises.Count > 0;
    }

    private static WorkoutSetInput ParseSetInput(WorkoutSetItem workoutSet)
    {
        var weightText = workoutSet.WeightText.Trim();
        var repetitionsText = workoutSet.RepetitionsText.Trim();

        if (!TryParseDecimal(weightText, out var weight))
        {
            throw new WorkoutValidationException(AppText.InvalidWeight);
        }

        var repetitions = 0;
        if (!string.IsNullOrWhiteSpace(repetitionsText) &&
            !int.TryParse(repetitionsText, NumberStyles.Integer, CultureInfo.CurrentCulture, out repetitions))
        {
            throw new WorkoutValidationException(AppText.InvalidRepetitions);
        }

        return new WorkoutSetInput(
            weight,
            repetitions,
            workoutSet.IsWarmup,
            workoutSet.Rpe,
            workoutSet.SetType);
    }

    private static bool TryParseDecimal(string text, out decimal value)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            value = 0;
            return true;
        }

        const NumberStyles styles =
            NumberStyles.AllowLeadingSign |
            NumberStyles.AllowDecimalPoint;

        return decimal.TryParse(text, styles, CultureInfo.CurrentCulture, out value) ||
               decimal.TryParse(text, styles, CultureInfo.InvariantCulture, out value) ||
               decimal.TryParse(
                   text.Replace(',', '.'),
                   styles,
                   CultureInfo.InvariantCulture,
                   out value);
    }

    private static string FormatPreviousPerformance(PreviousExercisePerformance? previous)
    {
        if (previous is null)
        {
            return AppText.NoPreviousWorkout;
        }

        return $"{AppText.PreviousPerformance} · {previous.CompletedAt.ToLocalTime():dd/MM}";
    }

    private Task TryRefreshPersonalRecordsForSetAsync(WorkoutSetItem workoutSet)
    {
        var exercise = Exercises.FirstOrDefault(item => item.Sets.Contains(workoutSet));
        return exercise is null
            ? Task.CompletedTask
            : TryRefreshPersonalRecordsAsync(exercise);
    }

    private async Task TryRefreshPersonalRecordsAsync(ActiveWorkoutExerciseItem exercise)
    {
        try
        {
            await RefreshPersonalRecordsAsync(exercise);
        }
        catch (Exception exception)
        {
            // Personal-record badges are derived UI state. A refresh failure must not make a
            // set mutation that was already committed look unsuccessful to the athlete.
            System.Diagnostics.Debug.WriteLine(exception);
        }
    }

    private async Task RefreshPersonalRecordsAsync(ActiveWorkoutExerciseItem exercise)
    {
        if (_session is null || exercise.ExerciseId is not { } exerciseId)
        {
            foreach (var workoutSet in exercise.Sets)
            {
                workoutSet.IsPersonalRecord = false;
            }

            return;
        }

        var recordSetIds = await statisticsService.GetWeightPersonalRecordSetIdsAsync(
            _session.Id,
            exerciseId);
        foreach (var matchingExercise in Exercises.Where(item => item.ExerciseId == exerciseId))
        {
            foreach (var workoutSet in matchingExercise.Sets)
            {
                workoutSet.IsPersonalRecord = recordSetIds.Contains(workoutSet.Id);
            }
        }
    }

    private void StartRestTimer(ActiveWorkoutExerciseItem exercise)
    {
        _restTimerExerciseId = exercise.Id;
        _restTimerEndsAt = DateTimeOffset.UtcNow.AddSeconds(exercise.RestTimerSeconds);
        RestTimerExerciseName = exercise.Name;
        IsRestTimerRunning = true;
        RefreshRestTimer();
    }

    private static string FormatCountdown(int totalSeconds)
    {
        var duration = TimeSpan.FromSeconds(totalSeconds);
        return duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}"
            : $"{duration.Minutes:00}:{duration.Seconds:00}";
    }

    private void RefreshWorkoutStats()
    {
        var sets = Exercises.SelectMany(item => item.Sets).ToList();
        var completedSets = sets.Where(item => item.IsCompleted).ToList();

        IncompleteSetCount = sets.Count(item => !item.IsCompleted);
        CompletedSetsText = completedSets.Count.ToString(CultureInfo.CurrentCulture);

        var volume = completedSets
            .Where(item => !item.IsWarmup)
            .Sum(item =>
            {
                TryParseDecimal(item.WeightText, out var weight);
                int.TryParse(
                    item.RepetitionsText,
                    NumberStyles.Integer,
                    CultureInfo.CurrentCulture,
                    out var repetitions);
                return weight * repetitions;
            });
        VolumeText = WorkoutDisplayFormatter.FormatVolume(volume);
    }
}
