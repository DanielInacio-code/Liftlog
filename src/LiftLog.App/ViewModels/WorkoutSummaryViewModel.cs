using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiftLog.App.Services;
using LiftLog.App.Resources.Strings;
using LiftLog.Core.Services;

namespace LiftLog.App.ViewModels;

public partial class WorkoutSummaryViewModel(
    IHistoryService historyService,
    IStatisticsService statisticsService,
    INavigationService navigationService) : BaseViewModel, IQueryAttributable
{
    private int _sessionId;

    [ObservableProperty]
    private string workoutName = string.Empty;

    [ObservableProperty]
    private string date = string.Empty;

    [ObservableProperty]
    private string duration = string.Empty;

    [ObservableProperty]
    private string comparisonContext = string.Empty;

    [ObservableProperty]
    private string completedSetCount = string.Empty;

    [ObservableProperty]
    private string volume = string.Empty;

    [ObservableProperty]
    private string personalRecordCount = string.Empty;

    [ObservableProperty]
    private string durationComparison = string.Empty;

    [ObservableProperty]
    private string volumeComparison = string.Empty;

    [ObservableProperty]
    private string completedSetComparison = string.Empty;

    [ObservableProperty]
    private bool hasPreviousWorkout;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("WorkoutSessionId", out var value))
        {
            _sessionId = Convert.ToInt32(value);
        }
    }

    public Task LoadAsync() => RunBusyAsync(async () =>
    {
        var session = await historyService.GetByIdAsync(_sessionId)
            ?? throw new InvalidOperationException("Completed workout not found.");

        WorkoutName = session.RoutineName;
        Date = WorkoutDisplayFormatter.FormatDate(session.StartedAt);
        var duration = historyService.CalculateDuration(session);
        var volume = historyService.CalculateVolume(session);
        var completedSets = historyService.CountCompletedSets(session);

        Duration = WorkoutDisplayFormatter.FormatDuration(duration);
        CompletedSetCount = completedSets.ToString();
        Volume = WorkoutDisplayFormatter.FormatVolume(volume);
        PersonalRecordCount = (await statisticsService
            .GetWeightPersonalRecordSetIdsAsync(session.Id)).Count.ToString();

        var previous = await historyService.GetPreviousCompletedForRoutineAsync(session.Id);
        HasPreviousWorkout = previous is not null;
        ComparisonContext = HasPreviousWorkout
            ? AppText.ComparedWithPreviousRoutineWorkout
            : AppText.FirstWorkoutInRoutine;

        if (previous is not null)
        {
            DurationComparison = FormatDurationComparison(
                duration - historyService.CalculateDuration(previous));
            VolumeComparison = FormatVolumeComparison(
                volume - historyService.CalculateVolume(previous));
            CompletedSetComparison = FormatCountComparison(
                completedSets - historyService.CountCompletedSets(previous));
        }
        else
        {
            DurationComparison = string.Empty;
            VolumeComparison = string.Empty;
            CompletedSetComparison = string.Empty;
        }
    }, AppText.FailedToLoadWorkoutSummary);

    private static string FormatDurationComparison(TimeSpan difference)
    {
        if (difference == TimeSpan.Zero)
        {
            return AppText.SameAsPreviousWorkout;
        }

        var absolute = difference.Duration();
        var value = absolute.TotalMinutes < 1
            ? $"{Math.Max(1, (int)Math.Round(absolute.TotalSeconds))} sec"
            : WorkoutDisplayFormatter.FormatDuration(absolute);
        return FormatSignedComparison(difference > TimeSpan.Zero, value);
    }

    private static string FormatVolumeComparison(decimal difference)
    {
        if (difference == 0)
        {
            return AppText.SameAsPreviousWorkout;
        }

        return FormatSignedComparison(
            difference > 0,
            WorkoutDisplayFormatter.FormatVolume(Math.Abs(difference)));
    }

    private static string FormatCountComparison(int difference)
    {
        if (difference == 0)
        {
            return AppText.SameAsPreviousWorkout;
        }

        return FormatSignedComparison(difference > 0, Math.Abs(difference).ToString());
    }

    private static string FormatSignedComparison(bool isIncrease, string value) =>
        $"{(isIncrease ? "+" : "-")}{value} {AppText.VersusPreviousWorkout}";

    [RelayCommand]
    private Task RetryAsync() => LoadAsync();

    [RelayCommand]
    private Task OpenDetailsAsync() =>
        navigationService.GoToAsync(
            NavigationRoutes.WorkoutDetails,
            new Dictionary<string, object> { ["WorkoutSessionId"] = _sessionId });

    [RelayCommand]
    private Task FinishAsync() => navigationService.GoToAsync(NavigationRoutes.Home);
}
