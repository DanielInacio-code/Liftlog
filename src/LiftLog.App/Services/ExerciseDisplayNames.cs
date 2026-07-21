using LiftLog.Core.Models;

namespace LiftLog.App.Services;

public static class ExerciseDisplayNames
{
    public static string For(MuscleGroup value) => value switch
    {
        MuscleGroup.Chest => "Chest",
        MuscleGroup.Back => "Back",
        MuscleGroup.Shoulders => "Shoulders",
        MuscleGroup.Biceps => "Biceps",
        MuscleGroup.Triceps => "Triceps",
        MuscleGroup.Legs => "Legs",
        MuscleGroup.Glutes => "Glutes",
        MuscleGroup.Calves => "Calves",
        MuscleGroup.Core => "Core",
        MuscleGroup.FullBody => "Full body",
        MuscleGroup.Cardio => "Cardio",
        _ => "Other"
    };

    public static string For(Equipment value) => value switch
    {
        Equipment.Barbell => "Barbell",
        Equipment.Dumbbell => "Dumbbell",
        Equipment.Machine => "Machine",
        Equipment.Cable => "Cable",
        Equipment.Bodyweight => "Bodyweight",
        Equipment.Kettlebell => "Kettlebell",
        Equipment.ResistanceBand => "Resistance band",
        Equipment.Other => "Other",
        _ => "None"
    };
}
