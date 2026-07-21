namespace LiftLog.Core.Services;

public sealed record PreviousSetPerformance(
    decimal Weight,
    int Repetitions,
    bool IsWarmup = false,
    int SetNumber = 0,
    double? Rpe = null);

public sealed record PreviousExercisePerformance(
    DateTimeOffset CompletedAt,
    IReadOnlyList<PreviousSetPerformance> Sets,
    IReadOnlyList<PreviousSetPerformance>? AllSets = null);
