using LiftLog.Core.Models;

namespace LiftLog.Core.Services;

public interface IWorkoutService
{
    event EventHandler<ActiveWorkoutChangedEventArgs>? ActiveWorkoutChanged;

    Task<WorkoutSession> StartAsync(int routineId, CancellationToken cancellationToken = default);

    Task<WorkoutSession?> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<ActiveWorkoutOverview?> GetActiveOverviewAsync(
        CancellationToken cancellationToken = default);

    Task<bool> HasActiveAsync(CancellationToken cancellationToken = default);

    Task<WorkoutExercise> AddExerciseAsync(
        int sessionId,
        int exerciseId,
        CancellationToken cancellationToken = default);

    Task<WorkoutExercise> ReplaceExerciseAsync(
        int workoutExerciseId,
        int exerciseId,
        CancellationToken cancellationToken = default);

    Task MoveExerciseAsync(
        int workoutExerciseId,
        int newPosition,
        CancellationToken cancellationToken = default);

    Task RemoveExerciseAsync(
        int workoutExerciseId,
        CancellationToken cancellationToken = default);

    Task<WorkoutExercise> SetRestTimerAsync(
        int workoutExerciseId,
        int seconds,
        CancellationToken cancellationToken = default);

    Task<WorkoutExercise> UpdateExerciseNotesAsync(
        int workoutExerciseId,
        string? notes,
        CancellationToken cancellationToken = default);

    Task<WorkoutSet> AddSetAsync(int workoutExerciseId, CancellationToken cancellationToken = default);

    Task<WorkoutSet> UpdateSetAsync(
        int setId,
        WorkoutSetInput input,
        CancellationToken cancellationToken = default);

    Task<WorkoutSet> SetCompletedAsync(
        int setId,
        bool isCompleted,
        CancellationToken cancellationToken = default);

    Task<WorkoutSet> UpdateSetAndCompletionAsync(
        int setId,
        WorkoutSetInput input,
        bool isCompleted,
        CancellationToken cancellationToken = default);

    Task DeleteSetAsync(int setId, CancellationToken cancellationToken = default);

    Task<WorkoutSession> CompleteAsync(int sessionId, CancellationToken cancellationToken = default);

    Task<WorkoutSession> CancelAsync(int sessionId, CancellationToken cancellationToken = default);

    Task<PreviousExercisePerformance?> GetPreviousPerformanceAsync(
        int exerciseId,
        int currentSessionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<int, PreviousExercisePerformance>> GetPreviousPerformancesAsync(
        IReadOnlyCollection<int> exerciseIds,
        int currentSessionId,
        CancellationToken cancellationToken = default);

    TimeSpan CalculateDuration(WorkoutSession session, DateTimeOffset? now = null);
}
