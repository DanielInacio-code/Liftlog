using LiftLog.Core.Models;

namespace LiftLog.Core.Data;

internal static class SeedExercises
{
    private static readonly DateTimeOffset SeedCreatedAt =
        new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly IReadOnlyList<SeedExerciseDefinition> Definitions =
    [
        Definition("Barbell bench press", MuscleGroup.Chest, Equipment.Barbell, "Supino com barra"),
        Definition("Dumbbell bench press", MuscleGroup.Chest, Equipment.Dumbbell, "Supino com halteres"),
        Definition("Barbell squat", MuscleGroup.Legs, Equipment.Barbell, "Agachamento com barra"),
        Definition("Deadlift", MuscleGroup.Back, Equipment.Barbell, "Peso morto"),
        Definition(
            "Bent Over Row",
            MuscleGroup.Back,
            Equipment.Barbell,
            "Bent Over Row (Barbell)",
            "Barbell row",
            "Remada com barra"),
        Definition("Lat Pulldown (Cable)", MuscleGroup.Back, Equipment.Cable, "Lat pulldown", "Puxada na polia"),
        Definition(
            "Shoulder Press (Dumbbell)",
            MuscleGroup.Shoulders,
            Equipment.Dumbbell,
            "Dumbbell shoulder press",
            "Desenvolvimento de ombros",
            "Seated Overhead Press (Dumbbell)"),
        Definition("Lateral Raise (Dumbbell)", MuscleGroup.Shoulders, Equipment.Dumbbell, "Dumbbell lateral raise", "Elevação lateral"),
        Definition("Dumbbell biceps curl", MuscleGroup.Biceps, Equipment.Dumbbell, "Curl de bíceps"),
        Definition("Leg Press (Machine)", MuscleGroup.Legs, Equipment.Machine, "Leg press"),
        Definition("Leg Extension (Machine)", MuscleGroup.Legs, Equipment.Machine, "Leg extension", "Extensão de pernas"),
        Definition(
            "Lying Leg Curl",
            MuscleGroup.Legs,
            Equipment.Machine,
            "Leg curl",
            "Flexão de pernas"),
        Definition(
            "Seated Leg Curl",
            MuscleGroup.Legs,
            Equipment.Machine,
            "Seated Leg Curl (Machine)"),
        Definition("Calf Extension (Machine)", MuscleGroup.Calves, Equipment.Machine, "Calf raise", "Elevação de gémeos"),
        // Exercises imported from the user's Hevy export on 15 July 2026.
        Definition("Triceps Pushdown", MuscleGroup.Triceps, Equipment.Cable),
        Definition("Single-Arm Triceps Pushdown", MuscleGroup.Triceps, Equipment.Cable),
        Definition("Preacher Curl (Dumbbell)", MuscleGroup.Biceps, Equipment.Dumbbell),
        Definition("Chest Press (Machine)", MuscleGroup.Chest, Equipment.Machine),
        Definition("Chest Press - Neutral Grip (Machine)", MuscleGroup.Chest, Equipment.Machine),
        Definition("Chest Fly (Machine)", MuscleGroup.Chest, Equipment.Machine),
        Definition("Chest Dip", MuscleGroup.Chest, Equipment.Bodyweight),
        Definition("Lateral Raise (Machine)", MuscleGroup.Shoulders, Equipment.Machine),
        Definition("Seated Incline Curl (Dumbbell)", MuscleGroup.Biceps, Equipment.Dumbbell),
        Definition("Hammer Curl (Dumbbell)", MuscleGroup.Biceps, Equipment.Dumbbell),
        Definition("Straight Leg Deadlift", MuscleGroup.Legs, Equipment.Barbell),
        Definition(
            "Chest Supported Row / T-Bar Row",
            MuscleGroup.Back,
            Equipment.Machine,
            "Chest Supported T-Bar Row (Machine)",
            "Chest Supported Incline Row (Dumbbell)"),
        Definition("Pullover (Machine)", MuscleGroup.Back, Equipment.Machine),
        Definition("Straight-Arm Pulldown", MuscleGroup.Back, Equipment.Cable),
        Definition("Single Arm Cable Row", MuscleGroup.Back, Equipment.Cable),
        Definition("Lateral Raise (Cable)", MuscleGroup.Shoulders, Equipment.Cable),
        Definition("Single Leg Press (Machine)", MuscleGroup.Legs, Equipment.Machine),
        Definition("Overhead Triceps Extension (Cable)", MuscleGroup.Triceps, Equipment.Cable),
        Definition("Hip Adduction (Machine)", MuscleGroup.Legs, Equipment.Machine),
        Definition("Preacher Curl (Machine)", MuscleGroup.Biceps, Equipment.Machine),
        Definition("Wide Row (Machine)", MuscleGroup.Back, Equipment.Machine, "Remada Aberta Maquina"),
        Definition("Lat Pulldown - Close Grip (Cable)", MuscleGroup.Back, Equipment.Cable),
        Definition(
            "Shoulder Press (Machine)",
            MuscleGroup.Shoulders,
            Equipment.Machine,
            "Seated Shoulder Press (Machine)"),
        Definition("Butterfly (Pec Deck)", MuscleGroup.Chest, Equipment.Machine),
        Definition(
            "Bayesian Curl (Cable)",
            MuscleGroup.Biceps,
            Equipment.Cable,
            "Behind the Back Curl (Cable)"),
        Definition("Hip Abduction (Machine)", MuscleGroup.Glutes, Equipment.Machine),
        Definition("Incline Bench Press (Dumbbell)", MuscleGroup.Chest, Equipment.Dumbbell),
        Definition("Decline Bench Press (Machine)", MuscleGroup.Chest, Equipment.Machine),
        Definition("Rear Delt Reverse Fly (Cable)", MuscleGroup.Shoulders, Equipment.Cable),
        Definition("Preacher Curl (Barbell)", MuscleGroup.Biceps, Equipment.Barbell),
        Definition("Hack Squat (Machine)", MuscleGroup.Legs, Equipment.Machine),
        Definition("Seated Cable Row - V Grip (Cable)", MuscleGroup.Back, Equipment.Cable),
        Definition("Face Pull", MuscleGroup.Shoulders, Equipment.Cable),
        Definition("Romanian Deadlift (Barbell)", MuscleGroup.Legs, Equipment.Barbell)
    ];

    public static IReadOnlyList<Exercise> Create() => Definitions
        .Select(definition => New(
            definition.EnglishName,
            definition.MuscleGroup,
            definition.Equipment))
        .ToList();

    public static IReadOnlyDictionary<string, string> LegacyNameTranslations { get; } =
        Definitions
            .SelectMany(definition => definition.Aliases.Select(alias => new
            {
                Alias = alias,
                CanonicalName = definition.EnglishName
            }))
            .Where(item => !string.Equals(
                item.Alias,
                item.CanonicalName,
                StringComparison.OrdinalIgnoreCase))
            .ToDictionary(
                item => item.Alias,
                item => item.CanonicalName,
                StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<string> RetiredPredefinedNames { get; } =
    [
        "Plank",
        "Prancha",
        "Triceps Extension (Dumbbell)",
        "Cable triceps extension"
    ];

    private static SeedExerciseDefinition Definition(
        string englishName,
        MuscleGroup muscleGroup,
        Equipment equipment,
        params string[] aliases) =>
        new(englishName, muscleGroup, equipment, aliases);

    private static Exercise New(string name, MuscleGroup muscleGroup, Equipment equipment) =>
        new()
        {
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            MuscleGroup = muscleGroup,
            Equipment = equipment,
            IsCustom = false,
            CreatedAt = SeedCreatedAt
        };

    private sealed record SeedExerciseDefinition(
        string EnglishName,
        MuscleGroup MuscleGroup,
        Equipment Equipment,
        IReadOnlyList<string> Aliases);
}
