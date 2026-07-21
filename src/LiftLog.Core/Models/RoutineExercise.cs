namespace LiftLog.Core.Models;

public sealed class RoutineExercise
{
    public int Id { get; set; }

    public int RoutineId { get; set; }

    public Routine Routine { get; set; } = null!;

    public int ExerciseId { get; set; }

    public Exercise Exercise { get; set; } = null!;

    public int Position { get; set; }

    public ICollection<RoutineSet> Sets { get; set; } = [];
}
