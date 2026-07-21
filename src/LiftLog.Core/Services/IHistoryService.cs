using LiftLog.Core.Models;

namespace LiftLog.Core.Services;

public interface IHistoryService
{
    Task<IReadOnlyList<WorkoutSession>> GetCompletedAsync(
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkoutSession>> GetRecentCompletedAsync(
        int maximumCount,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkoutSession>> GetCompletedInFinishedRangeAsync(
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkoutSession>> GetCompletedPageAsync(
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task<WorkoutSession?> GetByIdAsync(
        int sessionId,
        CancellationToken cancellationToken = default);

    Task<WorkoutSession?> GetPreviousCompletedForRoutineAsync(
        int sessionId,
        CancellationToken cancellationToken = default);

    TimeSpan CalculateDuration(WorkoutSession session);

    decimal CalculateVolume(WorkoutSession session);

    decimal CalculateVolume(WorkoutExercise exercise);

    int CountCompletedSets(WorkoutSession session);
}
