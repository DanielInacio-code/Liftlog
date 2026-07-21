using LiftLog.Core.Models;

namespace LiftLog.Core.Services;

public sealed record ExerciseInput(
    string Name,
    MuscleGroup MuscleGroup,
    Equipment Equipment,
    string? Instructions,
    string? ImagePath = null);
