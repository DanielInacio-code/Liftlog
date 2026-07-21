using LiftLog.Core.Models;
using LiftLog.App.Resources.Strings;
using LiftLog.App.Services;

namespace LiftLog.App.ViewModels;

public sealed class WorkoutDetailsSetItem(WorkoutSet workoutSet, bool isPersonalRecord)
{
    public string SetNumber { get; } = AppText.SetLabel(workoutSet.SetNumber);

    public string Performance { get; } =
        $"{WorkoutDisplayFormatter.FormatWeight(workoutSet.Weight)} × {workoutSet.Repetitions}";

    public string Type { get; } = workoutSet.IsWarmup ? AppText.Warmup : AppText.WorkingSet;

    public string Status { get; } = workoutSet.IsCompleted ? AppText.Completed : AppText.Incomplete;

    public bool IsPersonalRecord { get; } = isPersonalRecord;

    public string Achievement { get; } = isPersonalRecord ? AppText.NewWeightRecord : string.Empty;
}
