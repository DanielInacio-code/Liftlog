using LiftLog.App.Services;
using LiftLog.Core.Services;

namespace LiftLog.App.ViewModels;

public sealed class ExerciseProgressPointItem(ExerciseProgressPoint point)
{
    public string Date { get; } = WorkoutDisplayFormatter.FormatDate(point.StartedAt);

    public string MaximumWeight { get; } = WorkoutDisplayFormatter.FormatWeight(point.MaximumWeight);

    public string BestRepetitions { get; } = $"{point.BestRepetitions} reps";

    public string Volume { get; } = WorkoutDisplayFormatter.FormatVolume(point.Volume);
}
