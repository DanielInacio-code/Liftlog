using LiftLog.Core.Models;

namespace LiftLog.Core.Services;

public sealed record WorkoutSetInput(
    decimal Weight,
    int Repetitions,
    bool IsWarmup,
    double? Rpe = null,
    TrainingSetType SetType = TrainingSetType.Normal);
