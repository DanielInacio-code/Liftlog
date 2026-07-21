using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiftLog.App.Resources.Strings;
using LiftLog.App.Services;
using LiftLog.Core.Models;
using LiftLog.Core.Services;

namespace LiftLog.App.ViewModels;

public partial class ActiveWorkoutBannerViewModel : ObservableObject
{
    private static readonly TimeSpan StateFreshness = TimeSpan.FromSeconds(1);

    private readonly IWorkoutService _workoutService;
    private readonly INavigationService _navigationService;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly object _stateSync = new();
    private DateTimeOffset? _startedAt;
    private IDispatcherTimer? _durationTimer;
    private DateTimeOffset _lastStateUpdate = DateTimeOffset.MinValue;
    private int _activeViewCount;
    private long _stateVersion;
    private bool _hasLoaded;

    public ActiveWorkoutBannerViewModel(
        IWorkoutService workoutService,
        INavigationService navigationService)
    {
        _workoutService = workoutService;
        _navigationService = navigationService;
        _workoutService.ActiveWorkoutChanged += OnActiveWorkoutChanged;
    }

    [ObservableProperty]
    private bool isActive;

    [ObservableProperty]
    private string routineName = string.Empty;

    [ObservableProperty]
    private string statusText = string.Empty;

    public Task EnsureLoadedAsync()
    {
        lock (_stateSync)
        {
            if (_hasLoaded && DateTimeOffset.UtcNow - _lastStateUpdate < StateFreshness)
            {
                return Task.CompletedTask;
            }
        }

        return RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        long requestedVersion;
        lock (_stateSync)
        {
            requestedVersion = _stateVersion;
        }

        await _refreshGate.WaitAsync();

        try
        {
            long refreshVersion;
            lock (_stateSync)
            {
                // Another refresh or event completed while this call waited for
                // the gate, so its state already satisfies this request.
                if (requestedVersion != _stateVersion)
                {
                    return;
                }

                refreshVersion = _stateVersion;
            }

            var workout = await _workoutService.GetActiveOverviewAsync();
            TryApplyRefresh(workout, refreshVersion);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(exception);

            lock (_stateSync)
            {
                // Keep a valid event/cache result visible when a refresh fails.
                // With no known state, leave the banner at its initial inactive state
                // and allow the next Loaded/Appearing refresh to retry.
                if (!_hasLoaded)
                {
                    _lastStateUpdate = DateTimeOffset.MinValue;
                }
            }
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    public void Activate()
    {
        _activeViewCount++;
        if (_activeViewCount != 1)
        {
            return;
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        _durationTimer ??= CreateDurationTimer(dispatcher);
        _durationTimer.Start();
    }

    public void Deactivate()
    {
        if (_activeViewCount > 0)
        {
            _activeViewCount--;
        }

        if (_activeViewCount == 0)
        {
            _durationTimer?.Stop();
        }
    }

    public void RefreshDuration()
    {
        if (_startedAt is null)
        {
            return;
        }

        var elapsed = DateTimeOffset.UtcNow - _startedAt.Value;
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        RoutineName = $"{AppText.WorkoutInProgress} · {FormatElapsed(elapsed)}";
    }

    private void OnActiveWorkoutChanged(
        object? sender,
        ActiveWorkoutChangedEventArgs eventArgs)
    {
        long eventVersion;
        lock (_stateSync)
        {
            eventVersion = ++_stateVersion;
            _hasLoaded = true;
            _lastStateUpdate = DateTimeOffset.UtcNow;
        }

        void ApplyEvent() => TryApplyEvent(eventArgs.Workout, eventVersion);

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher?.IsDispatchRequired == true)
        {
            dispatcher.Dispatch(ApplyEvent);
            return;
        }

        ApplyEvent();
    }

    [RelayCommand]
    private Task ContinueWorkoutAsync() =>
        _navigationService.GoToAsync(NavigationRoutes.ActiveWorkout);

    private void ApplyWorkout(WorkoutSession? workout)
    {
        IsActive = workout is not null;
        if (workout is null)
        {
            RoutineName = string.Empty;
            StatusText = string.Empty;
            _startedAt = null;
            return;
        }

        _startedAt = workout.StartedAt;
        var orderedExercises = workout.Exercises
            .OrderBy(exercise => exercise.Position)
            .ToList();
        var currentExercise = orderedExercises.FirstOrDefault(exercise =>
                exercise.Sets.Count == 0 || exercise.Sets.Any(set => !set.IsCompleted))
            ?? orderedExercises.LastOrDefault();

        StatusText = currentExercise is null
            ? workout.RoutineName
            : $"{workout.RoutineName} · {currentExercise.ExerciseName}";
        RefreshDuration();
    }

    private void ApplyWorkout(ActiveWorkoutOverview? workout)
    {
        IsActive = workout is not null;
        if (workout is null)
        {
            RoutineName = string.Empty;
            StatusText = string.Empty;
            _startedAt = null;
            return;
        }

        _startedAt = workout.StartedAt;
        StatusText = string.IsNullOrWhiteSpace(workout.CurrentExerciseName)
            ? workout.RoutineName
            : $"{workout.RoutineName} · {workout.CurrentExerciseName}";
        RefreshDuration();
    }

    private void TryApplyRefresh(ActiveWorkoutOverview? workout, long refreshVersion)
    {
        lock (_stateSync)
        {
            if (refreshVersion != _stateVersion)
            {
                return;
            }

            // A refresh that started after the last event is the newest snapshot.
            // Advancing the version also invalidates an older event callback that
            // may still be queued on the UI dispatcher.
            _stateVersion++;
            ApplyWorkout(workout);
            _hasLoaded = true;
            _lastStateUpdate = DateTimeOffset.UtcNow;
        }
    }

    private void TryApplyEvent(WorkoutSession? workout, long eventVersion)
    {
        lock (_stateSync)
        {
            if (eventVersion != _stateVersion)
            {
                return;
            }

            ApplyWorkout(workout);
        }
    }

    private IDispatcherTimer CreateDurationTimer(IDispatcher dispatcher)
    {
        var timer = dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(1);
        timer.Tick += (_, _) => RefreshDuration();
        return timer;
    }

    private static string FormatElapsed(TimeSpan elapsed) => elapsed.TotalHours >= 1
        ? $"{(int)elapsed.TotalHours}h {elapsed.Minutes:00}m"
        : elapsed.TotalMinutes >= 1
            ? $"{elapsed.Minutes}m {elapsed.Seconds:00}s"
            : $"{elapsed.Seconds}s";
}
