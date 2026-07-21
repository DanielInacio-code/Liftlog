using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiftLog.App.Controls;
using LiftLog.App.Resources.Strings;
using LiftLog.App.Services;
using LiftLog.Core.Models;
using LiftLog.Core.Services;

namespace LiftLog.App.ViewModels;

public partial class ProfileViewModel(
    IHistoryService historyService,
    INavigationService navigationService) : BaseViewModel
{
    private IReadOnlyList<WorkoutSession> _completedWorkouts = [];
    private TrainingMetric _selectedMetric = TrainingMetric.Volume;

    public IReadOnlyList<ProfilePeriodOption> PeriodOptions { get; } =
    [
        new(AppText.LastMonth, ProfilePeriod.LastMonth),
        new(AppText.LastThreeMonths, ProfilePeriod.ThreeMonths),
        new(AppText.LastYear, ProfilePeriod.OneYear)
    ];

    [ObservableProperty]
    private ProfilePeriodOption? selectedPeriod;

    [ObservableProperty]
    private string totalWorkouts = "0";

    [ObservableProperty]
    private string totalVolume = "0 kg";

    [ObservableProperty]
    private string totalDuration = "0 min";

    [ObservableProperty]
    private string chartValue = "0 kg";

    [ObservableProperty]
    private IReadOnlyList<ChartDataPoint> chartPoints = [];

    [ObservableProperty]
    private bool isChartEmpty = true;

    [ObservableProperty]
    private bool isVolumeSelected = true;

    [ObservableProperty]
    private bool isDurationSelected;

    [ObservableProperty]
    private bool isSetsSelected;

    public Task LoadAsync() => RunBusyAsync(async () =>
    {
        _completedWorkouts = await historyService.GetCompletedAsync();
        TotalWorkouts = _completedWorkouts.Count.ToString();
        TotalVolume = WorkoutDisplayFormatter.FormatCompactVolume(
            _completedWorkouts.Sum(historyService.CalculateVolume));
        TotalDuration = FormatDuration(
            TimeSpan.FromTicks(_completedWorkouts.Sum(workout =>
                historyService.CalculateDuration(workout).Ticks)));

        SelectedPeriod ??= PeriodOptions[1];
        RefreshChart();
    }, AppText.FailedToLoadProfile);

    public void RefreshPeriod() => RefreshChart();

    [RelayCommand]
    private void SelectMetric(string metricName)
    {
        if (!Enum.TryParse<TrainingMetric>(metricName, out var metric))
        {
            return;
        }

        _selectedMetric = metric;
        IsVolumeSelected = metric == TrainingMetric.Volume;
        IsDurationSelected = metric == TrainingMetric.Duration;
        IsSetsSelected = metric == TrainingMetric.Sets;
        RefreshChart();
    }

    [RelayCommand]
    private Task RetryAsync() => LoadAsync();

    [RelayCommand]
    private Task OpenExerciseProgressAsync() =>
        navigationService.GoToAsync(NavigationRoutes.ExerciseProgress);

    [RelayCommand]
    private Task OpenExercisesAsync() =>
        navigationService.GoToAsync(NavigationRoutes.Exercises);

    [RelayCommand]
    private Task OpenCalendarAsync() =>
        navigationService.GoToAsync(NavigationRoutes.Calendar);

    [RelayCommand]
    private Task OpenMeasurementsAsync() =>
        navigationService.GoToAsync(NavigationRoutes.Measurements);

    [RelayCommand]
    private Task OpenSettingsAsync() =>
        navigationService.GoToAsync(NavigationRoutes.Settings);

    private void RefreshChart()
    {
        if (SelectedPeriod is null)
        {
            return;
        }

        var buckets = CreateBuckets(SelectedPeriod.Period, DateTimeOffset.Now);
        var points = new List<ChartDataPoint>(buckets.Count);
        foreach (var bucket in buckets)
        {
            var workouts = _completedWorkouts
                .Where(workout =>
                {
                    var localStart = workout.StartedAt.ToLocalTime();
                    return localStart >= bucket.Start && localStart < bucket.End;
                })
                .ToList();
            points.Add(new ChartDataPoint(bucket.Label, CalculateMetric(workouts)));
        }

        ChartPoints = points;
        IsChartEmpty = points.All(point => point.Value <= 0);
        var total = points.Sum(point => point.Value);
        ChartValue = _selectedMetric switch
        {
            TrainingMetric.Volume => WorkoutDisplayFormatter.FormatVolume((decimal)total),
            TrainingMetric.Duration => FormatDuration(TimeSpan.FromMinutes(total)),
            _ => total.ToString("0")
        };
    }

    private double CalculateMetric(IEnumerable<WorkoutSession> workouts) =>
        _selectedMetric switch
        {
            TrainingMetric.Volume => (double)workouts.Sum(historyService.CalculateVolume),
            TrainingMetric.Duration => workouts.Sum(workout =>
                historyService.CalculateDuration(workout).TotalMinutes),
            _ => workouts.Sum(historyService.CountCompletedSets)
        };

    private static string FormatDuration(TimeSpan duration) =>
        duration <= TimeSpan.Zero
            ? "0 min"
            : WorkoutDisplayFormatter.FormatDuration(duration);

    private static IReadOnlyList<ProfileChartBucket> CreateBuckets(
        ProfilePeriod period,
        DateTimeOffset now)
    {
        var localNow = now.ToLocalTime();
        if (period == ProfilePeriod.OneYear)
        {
            var firstMonth = new DateTimeOffset(
                localNow.Year,
                localNow.Month,
                1,
                0,
                0,
                0,
                localNow.Offset).AddMonths(-11);
            return Enumerable.Range(0, 12)
                .Select(index =>
                {
                    var start = firstMonth.AddMonths(index);
                    return new ProfileChartBucket(start, start.AddMonths(1), start.ToString("MMM"));
                })
                .ToList();
        }

        var daysSinceMonday = ((int)localNow.DayOfWeek + 6) % 7;
        var currentWeek = new DateTimeOffset(
            localNow.Year,
            localNow.Month,
            localNow.Day,
            0,
            0,
            0,
            localNow.Offset).AddDays(-daysSinceMonday);
        var count = period == ProfilePeriod.LastMonth ? 4 : 12;
        var firstWeek = currentWeek.AddDays(-7 * (count - 1));
        return Enumerable.Range(0, count)
            .Select(index =>
            {
                var start = firstWeek.AddDays(index * 7);
                return new ProfileChartBucket(start, start.AddDays(7), start.ToString("dd MMM"));
            })
            .ToList();
    }

    private sealed record ProfileChartBucket(
        DateTimeOffset Start,
        DateTimeOffset End,
        string Label);
}
