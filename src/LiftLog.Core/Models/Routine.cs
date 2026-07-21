namespace LiftLog.Core.Models;

public sealed class Routine
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string NormalizedName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<RoutineExercise> Exercises { get; set; } = [];
}
