namespace LiftLog.Core.Models;

public sealed class Exercise
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string NormalizedName { get; set; } = string.Empty;

    public MuscleGroup MuscleGroup { get; set; }

    public Equipment Equipment { get; set; }

    public string? Instructions { get; set; }

    public string? ImagePath { get; set; }

    public bool IsCustom { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
