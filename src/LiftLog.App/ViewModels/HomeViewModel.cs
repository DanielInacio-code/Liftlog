using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiftLog.App.Services;
using LiftLog.App.Resources.Strings;
using LiftLog.Core.Services;

namespace LiftLog.App.ViewModels;

public partial class HomeViewModel(
    IWorkoutService workoutService,
    IHistoryService historyService,
    IRoutineService routineService,
    INavigationService navigationService) : BaseViewModel
{
    [ObservableProperty]
    private ObservableCollection<RoutineListItem> featuredRoutines = [];

    [ObservableProperty]
    private ObservableCollection<HomeWeekDayItem> weekDays = [];

    [ObservableProperty]
    private bool hasFeaturedRoutines;

    [ObservableProperty]
    private bool hasNoFeaturedRoutines = true;

    [ObservableProperty]
    private string weeklyWorkoutCount = "0";

    [ObservableProperty]
    private string weeklyVolume = WorkoutDisplayFormatter.FormatVolume(0);

    [ObservableProperty]
    private string weeklyDuration = WorkoutDisplayFormatter.FormatDuration(TimeSpan.Zero);

    [ObservableProperty]
    private bool hasRecentWorkout;

    [ObservableProperty]
    private string recentWorkoutName = string.Empty;

    [ObservableProperty]
    private string recentWorkoutDetails = string.Empty;

    private bool _hasActiveWorkout;
    private int? _recentWorkoutId;

    public Task LoadAsync() => RunBusyAsync(async () =>
    {
        var now = DateTimeOffset.Now;
        var startOfToday = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, now.Offset);
        var daysSinceMonday = ((int)now.DayOfWeek + 6) % 7;
        var weekStart = startOfToday.AddDays(-daysSinceMonday);
        var weekEnd = weekStart.AddDays(7);

        // Microsoft.Data.Sqlite executes its ADO.NET async methods synchronously.
        // Each service call creates its own DbContext, so these independent reads
        // can safely run away from the UI thread and in parallel.
        var hasActiveWorkoutTask = Task.Run(() => workoutService.HasActiveAsync());
        var routinesTask = Task.Run(() => routineService.GetAllAsync());
        var recentWorkoutsTask = Task.Run(() => historyService.GetRecentCompletedAsync(1));
        var weeklyWorkoutsTask = Task.Run(() =>
            historyService.GetCompletedInFinishedRangeAsync(weekStart, weekEnd));

        await Task.WhenAll(
            hasActiveWorkoutTask,
            routinesTask,
            recentWorkoutsTask,
            weeklyWorkoutsTask);

        _hasActiveWorkout = await hasActiveWorkoutTask;

        var routines = await routinesTask;
        FeaturedRoutines = new ObservableCollection<RoutineListItem>(
            routines.Select(routine => new RoutineListItem(routine)));

        HasFeaturedRoutines = FeaturedRoutines.Count > 0;
        HasNoFeaturedRoutines = !HasFeaturedRoutines;

        var recent = (await recentWorkoutsTask).FirstOrDefault();
        _recentWorkoutId = recent?.Id;
        HasRecentWorkout = recent is not null;
        RecentWorkoutName = recent?.RoutineName ?? string.Empty;
        RecentWorkoutDetails = recent is null
            ? string.Empty
            : $"{WorkoutDisplayFormatter.FormatDate(recent.StartedAt)} · {WorkoutDisplayFormatter.FormatVolume(historyService.CalculateVolume(recent))}";

        var weeklyWorkouts = await weeklyWorkoutsTask;

        WeeklyWorkoutCount = weeklyWorkouts.Count.ToString(CultureInfo.InvariantCulture);
        WeeklyVolume = WorkoutDisplayFormatter.FormatVolume(
            weeklyWorkouts.Sum(historyService.CalculateVolume));
        WeeklyDuration = WorkoutDisplayFormatter.FormatDuration(
            weeklyWorkouts.Aggregate(
                TimeSpan.Zero,
                (duration, workout) => duration + historyService.CalculateDuration(workout)));

        var weekDays = new List<HomeWeekDayItem>(7);
        for (var dayOffset = 0; dayOffset < 7; dayOffset++)
        {
            var date = weekStart.AddDays(dayOffset);
            var hasWorkout = weeklyWorkouts.Any(workout =>
                workout.FinishedAt?.ToLocalTime().Date == date.Date);
            weekDays.Add(new HomeWeekDayItem(
                date.ToString("ddd", CultureInfo.InvariantCulture).ToUpperInvariant(),
                hasWorkout,
                date.Date == startOfToday.Date));
        }

        WeekDays = new ObservableCollection<HomeWeekDayItem>(weekDays);
    }, AppText.FailedToCheckActiveWorkout);

    [RelayCommand]
    private Task RetryAsync() => LoadAsync();

    [RelayCommand]
    private Task OpenRoutinesAsync() =>
        navigationService.GoToAsync(NavigationRoutes.Routines);

    [RelayCommand]
    private Task StartRoutineAsync(RoutineListItem routine) => RunBusyAsync(async () =>
    {
        _hasActiveWorkout = await workoutService.HasActiveAsync();
        if (_hasActiveWorkout)
        {
            ErrorMessage = AppText.WorkoutAlreadyInProgress;
            return;
        }

        await navigationService.GoToAsync(
            NavigationRoutes.ActiveWorkout,
            new Dictionary<string, object> { ["RoutineId"] = routine.Id });
    }, AppText.FailedToCheckActiveWorkout);

    [RelayCommand]
    private Task OpenRecentWorkoutAsync()
    {
        if (_recentWorkoutId is null)
        {
            return Task.CompletedTask;
        }

        return navigationService.GoToAsync(
            NavigationRoutes.WorkoutDetails,
            new Dictionary<string, object> { ["WorkoutSessionId"] = _recentWorkoutId.Value });
    }

}

public sealed record HomeWeekDayItem(
    string Label,
    bool IsWorkoutDay,
    bool IsToday);
