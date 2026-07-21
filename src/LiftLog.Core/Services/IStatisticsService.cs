namespace LiftLog.Core.Services;

public interface IStatisticsService
{
    Task<ExerciseProgress> GetExerciseProgressAsync(
        int exerciseId,
        CancellationToken cancellationToken = default);

    Task<bool> IsWeightPersonalRecordAsync(
        int workoutSetId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlySet<int>> GetWeightPersonalRecordSetIdsAsync(
        int workoutSessionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlySet<int>> GetWeightPersonalRecordSetIdsAsync(
        int workoutSessionId,
        int exerciseId,
        CancellationToken cancellationToken = default);

    decimal CalculateVolume(IEnumerable<LiftLog.Core.Models.WorkoutSet> sets);

    decimal CalculateMaximumWeight(IEnumerable<LiftLog.Core.Models.WorkoutSet> sets);

    int CalculateBestRepetitions(IEnumerable<LiftLog.Core.Models.WorkoutSet> sets);
}
