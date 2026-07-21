namespace LiftLog.Core.Models;

public sealed class WorkoutSession
{
    public int Id { get; set; }

    public int? RoutineId { get; set; }

    public Routine? Routine { get; set; }

    public string RoutineName { get; set; } = string.Empty;

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    public string? Notes { get; set; }

    public WorkoutStatus Status { get; set; }

    public ICollection<WorkoutExercise> Exercises { get; set; } = [];
}
