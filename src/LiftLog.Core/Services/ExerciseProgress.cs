namespace LiftLog.Core.Services;

public sealed record ExerciseProgressPoint(
    int WorkoutSessionId,
    DateTimeOffset StartedAt,
    decimal MaximumWeight,
    int BestRepetitions,
    decimal Volume);

public sealed record ExerciseProgress(
    int ExerciseId,
    string ExerciseName,
    decimal BestWeight,
    int BestRepetitions,
    decimal TotalVolume,
    int WorkoutCount,
    IReadOnlyList<ExerciseProgressPoint> Points);
