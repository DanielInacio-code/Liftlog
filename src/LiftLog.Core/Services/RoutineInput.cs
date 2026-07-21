namespace LiftLog.Core.Services;

using LiftLog.Core.Models;

public sealed record RoutineSetInput(
    decimal Weight,
    int Repetitions,
    bool IsWarmup,
    double? Rpe = null,
    TrainingSetType SetType = TrainingSetType.Normal);

public sealed record RoutineExerciseInput(
    int ExerciseId,
    IReadOnlyList<RoutineSetInput> Sets);

public sealed record RoutineInput(
    string Name,
    IReadOnlyList<int> ExerciseIds,
    IReadOnlyList<RoutineExerciseInput>? ExercisePlans = null);
