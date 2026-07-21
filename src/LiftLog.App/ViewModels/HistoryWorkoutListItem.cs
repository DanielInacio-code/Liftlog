using LiftLog.App.Services;
using LiftLog.App.Resources.Strings;
using LiftLog.Core.Models;
using LiftLog.Core.Services;

namespace LiftLog.App.ViewModels;

public sealed class HistoryWorkoutListItem(WorkoutSession session, IHistoryService historyService)
{
    public int Id { get; } = session.Id;

    public string Name { get; } = session.RoutineName;

    public string Date { get; } = WorkoutDisplayFormatter.FormatDate(session.StartedAt);

    public string Duration { get; } =
        WorkoutDisplayFormatter.FormatDuration(historyService.CalculateDuration(session));

    public string Volume { get; } =
        WorkoutDisplayFormatter.FormatVolume(historyService.CalculateVolume(session));

    public string Summary { get; } = CreateSummary(session, historyService);

    private static string CreateSummary(WorkoutSession session, IHistoryService historyService)
    {
        var setCount = historyService.CountCompletedSets(session);
        var exerciseCount = session.Exercises.Count;
        var sets = AppText.CompletedSetCount(setCount);
        var exercises = AppText.ExerciseCount(exerciseCount);
        return $"{sets} · {exercises}";
    }
}
