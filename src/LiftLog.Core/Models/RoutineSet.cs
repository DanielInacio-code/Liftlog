namespace LiftLog.Core.Models;

public sealed class RoutineSet
{
    public int Id { get; set; }

    public int RoutineExerciseId { get; set; }

    public RoutineExercise RoutineExercise { get; set; } = null!;

    public int SetNumber { get; set; }

    public decimal Weight { get; set; }

    public int Repetitions { get; set; }

    public bool IsWarmup { get; set; }

    public TrainingSetType SetType { get; set; }

    public double? Rpe { get; set; }
}
