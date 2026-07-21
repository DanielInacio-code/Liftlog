using LiftLog.App.Services;
using LiftLog.Core.Models;

namespace LiftLog.App.ViewModels;

public sealed class ExerciseListItem(Exercise exercise)
{
    public int Id { get; } = exercise.Id;

    public string Name { get; } = exercise.Name;

    public string Details { get; } =
        $"{ExerciseDisplayNames.For(exercise.MuscleGroup)} · {ExerciseDisplayNames.For(exercise.Equipment)}";

    public string TypeLabel { get; } = exercise.IsCustom
        ? Resources.Strings.AppText.CustomExercise
        : Resources.Strings.AppText.PredefinedExercise;

    public bool IsCustom { get; } = exercise.IsCustom;

    public string ImageSource { get; } = ExerciseImageSource.ForThumbnail(exercise);
}
