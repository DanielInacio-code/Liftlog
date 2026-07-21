namespace LiftLog.Core.Models;

public sealed class WorkoutExercise
{
    public int Id { get; set; }

    public int WorkoutSessionId { get; set; }

    public WorkoutSession WorkoutSession { get; set; } = null!;

    public int? ExerciseId { get; set; }

    public Exercise? Exercise { get; set; }

    public string ExerciseName { get; set; } = string.Empty;

    public int Position { get; set; }

    public string? Notes { get; set; }

    public int RestTimerSeconds { get; set; }

    public ICollection<WorkoutSet> Sets { get; set; } = [];
}
