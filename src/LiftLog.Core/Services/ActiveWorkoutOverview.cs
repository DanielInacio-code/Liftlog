namespace LiftLog.Core.Services;

public sealed record ActiveWorkoutOverview(
    DateTimeOffset StartedAt,
    string RoutineName,
    string? CurrentExerciseName);
