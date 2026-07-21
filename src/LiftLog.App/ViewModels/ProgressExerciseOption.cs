using LiftLog.Core.Models;

namespace LiftLog.App.ViewModels;

public sealed class ProgressExerciseOption(Exercise exercise)
{
    public int Id { get; } = exercise.Id;

    public string Name { get; } = exercise.Name;
}
