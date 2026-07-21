using LiftLog.Core.Models;
using LiftLog.App.Resources.Strings;

namespace LiftLog.App.ViewModels;

public sealed class RoutineListItem(Routine routine)
{
    public int Id { get; } = routine.Id;

    public string Name { get; } = routine.Name;

    public string Initial { get; } = string.IsNullOrWhiteSpace(routine.Name)
        ? "R"
        : routine.Name[..1].ToUpperInvariant();

    public string ExerciseCount { get; } = routine.Exercises.Count == 0
        ? AppText.NoExercisesCount
        : AppText.ExerciseCount(routine.Exercises.Count);

    public string Preview { get; } = CreatePreview(routine);

    public bool CanStart { get; } = routine.Exercises.Count > 0;

    private static string CreatePreview(Routine routine)
    {
        var names = routine.Exercises
            .OrderBy(item => item.Position)
            .Select(item => item.Exercise.Name)
            .Take(3)
            .ToList();

        if (names.Count == 0)
        {
            return AppText.AddExercisesToRoutine;
        }

        var preview = string.Join(" · ", names);
        return routine.Exercises.Count > names.Count ? $"{preview} · …" : preview;
    }
}
