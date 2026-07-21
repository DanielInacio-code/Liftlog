namespace LiftLog.Core.Models;

public sealed class WorkoutSet
{
    public int Id { get; set; }

    public int WorkoutExerciseId { get; set; }

    public WorkoutExercise WorkoutExercise { get; set; } = null!;

    public int SetNumber { get; set; }

    public decimal Weight { get; set; }

    public int Repetitions { get; set; }

    public bool IsWarmup { get; set; }

    public TrainingSetType SetType { get; set; }

    public double? Rpe { get; set; }

    public bool IsCompleted { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
}
