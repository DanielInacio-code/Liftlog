using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiftLog.App.Services;
using LiftLog.App.Resources.Strings;
using LiftLog.Core.Services;

namespace LiftLog.App.ViewModels;

public partial class WorkoutDetailsViewModel(
    IHistoryService historyService,
    IStatisticsService statisticsService)
    : BaseViewModel, IQueryAttributable
{
    private int _sessionId;

    private ObservableCollection<WorkoutDetailsExerciseItem> _exercises = [];

    public ObservableCollection<WorkoutDetailsExerciseItem> Exercises
    {
        get => _exercises;
        private set => SetProperty(ref _exercises, value);
    }

    [ObservableProperty]
    private string workoutName = string.Empty;

    [ObservableProperty]
    private string date = string.Empty;

    [ObservableProperty]
    private string duration = string.Empty;

    [ObservableProperty]
    private string volume = string.Empty;

    [ObservableProperty]
    private string completedSets = string.Empty;

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
        Duration = WorkoutDisplayFormatter.FormatDuration(historyService.CalculateDuration(session));
        Volume = WorkoutDisplayFormatter.FormatVolume(historyService.CalculateVolume(session));
        CompletedSets = historyService.CountCompletedSets(session).ToString();
        var personalRecordSetIds = await statisticsService
            .GetWeightPersonalRecordSetIdsAsync(session.Id);

        Exercises = new ObservableCollection<WorkoutDetailsExerciseItem>(
            session.Exercises
                .OrderBy(item => item.Position)
                .Select(exercise => new WorkoutDetailsExerciseItem(
                    exercise,
                    historyService,
                    personalRecordSetIds)));
    }, AppText.FailedToLoadWorkoutDetails);

    [RelayCommand]
    private Task RetryAsync() => LoadAsync();
}
