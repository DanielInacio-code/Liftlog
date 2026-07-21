using LiftLog.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LiftLog.Core.Data;

public sealed class LiftLogDbContext(DbContextOptions<LiftLogDbContext> options) : DbContext(options)
{
    public DbSet<Exercise> Exercises => Set<Exercise>();

    public DbSet<Routine> Routines => Set<Routine>();

    public DbSet<RoutineExercise> RoutineExercises => Set<RoutineExercise>();

    public DbSet<RoutineSet> RoutineSets => Set<RoutineSet>();

    public DbSet<WorkoutSession> WorkoutSessions => Set<WorkoutSession>();

    public DbSet<WorkoutExercise> WorkoutExercises => Set<WorkoutExercise>();

    public DbSet<WorkoutSet> WorkoutSets => Set<WorkoutSet>();

    public DbSet<BodyMeasurement> BodyMeasurements => Set<BodyMeasurement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var nullableDateTimeOffsetConverter = new ValueConverter<DateTimeOffset?, long?>(
            value => value.HasValue ? value.Value.ToUnixTimeMilliseconds() : null,
            value => value.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(value.Value) : null);

        var exercise = modelBuilder.Entity<Exercise>();

        exercise.ToTable("Exercises", table =>
            table.HasCheckConstraint(
                "CK_Exercises_Name_Length",
                "length(trim(Name)) BETWEEN 2 AND 100"));

        exercise.HasKey(item => item.Id);

        exercise.Property(item => item.Name)
            .IsRequired()
            .HasMaxLength(100)
            .UseCollation("NOCASE");

        exercise.Property(item => item.NormalizedName)
            .IsRequired()
            .HasMaxLength(100);

        exercise.Property(item => item.MuscleGroup)
            .HasConversion<string>()
            .HasMaxLength(32);

        exercise.Property(item => item.Equipment)
            .HasConversion<string>()
            .HasMaxLength(32);

        exercise.Property(item => item.Instructions)
            .HasMaxLength(2000);

        exercise.Property(item => item.ImagePath)
            .HasMaxLength(1024);

        exercise.Property(item => item.CreatedAt)
            .HasConversion(
                value => value.ToUnixTimeMilliseconds(),
                value => DateTimeOffset.FromUnixTimeMilliseconds(value));

        exercise.HasIndex(item => item.MuscleGroup);

        exercise.HasIndex(item => item.NormalizedName)
            .IsUnique();

        var routine = modelBuilder.Entity<Routine>();

        routine.ToTable("Routines", table =>
            table.HasCheckConstraint(
                "CK_Routines_Name_Length",
                "length(trim(Name)) BETWEEN 2 AND 100"));

        routine.HasKey(item => item.Id);

        routine.Property(item => item.Name)
            .IsRequired()
            .HasMaxLength(100)
            .UseCollation("NOCASE");

        routine.Property(item => item.NormalizedName)
            .IsRequired()
            .HasMaxLength(100);

        routine.Property(item => item.CreatedAt)
            .HasConversion(
                value => value.ToUnixTimeMilliseconds(),
                value => DateTimeOffset.FromUnixTimeMilliseconds(value));

        routine.Property(item => item.UpdatedAt)
            .HasConversion(
                value => value.ToUnixTimeMilliseconds(),
                value => DateTimeOffset.FromUnixTimeMilliseconds(value));

        routine.HasIndex(item => item.NormalizedName)
            .IsUnique();

        routine.HasIndex(item => item.UpdatedAt);

        var routineExercise = modelBuilder.Entity<RoutineExercise>();

        routineExercise.ToTable("RoutineExercises", table =>
            table.HasCheckConstraint(
                "CK_RoutineExercises_Position",
                "Position >= 0"));

        routineExercise.HasKey(item => item.Id);

        routineExercise.HasOne(item => item.Routine)
            .WithMany(item => item.Exercises)
            .HasForeignKey(item => item.RoutineId)
            .OnDelete(DeleteBehavior.Cascade);

        routineExercise.HasOne(item => item.Exercise)
            .WithMany()
            .HasForeignKey(item => item.ExerciseId)
            .OnDelete(DeleteBehavior.Cascade);

        routineExercise.HasIndex(item => new { item.RoutineId, item.ExerciseId })
            .IsUnique();

        routineExercise.HasIndex(item => new { item.RoutineId, item.Position });

        var routineSet = modelBuilder.Entity<RoutineSet>();

        routineSet.ToTable("RoutineSets", table =>
        {
            table.HasCheckConstraint("CK_RoutineSets_SetNumber", "SetNumber > 0");
            table.HasCheckConstraint("CK_RoutineSets_Weight", "Weight >= 0");
            table.HasCheckConstraint("CK_RoutineSets_Repetitions", "Repetitions >= 0");
            table.HasCheckConstraint("CK_RoutineSets_Rpe", "Rpe IS NULL OR Rpe BETWEEN 0 AND 10");
        });

        routineSet.HasKey(item => item.Id);

        routineSet.Property(item => item.Weight)
            .HasConversion(
                value => (long)(value * 1000m),
                value => value / 1000m);

        routineSet.Property(item => item.SetType)
            .HasConversion<string>()
            .HasMaxLength(16);

        routineSet.HasOne(item => item.RoutineExercise)
            .WithMany(item => item.Sets)
            .HasForeignKey(item => item.RoutineExerciseId)
            .OnDelete(DeleteBehavior.Cascade);

        routineSet.HasIndex(item => new { item.RoutineExerciseId, item.SetNumber })
            .IsUnique();

        var workoutSession = modelBuilder.Entity<WorkoutSession>();

        workoutSession.ToTable("WorkoutSessions");
        workoutSession.HasKey(item => item.Id);

        workoutSession.Property(item => item.RoutineName)
            .IsRequired()
            .HasMaxLength(100);

        workoutSession.Property(item => item.StartedAt)
            .HasConversion(
                value => value.ToUnixTimeMilliseconds(),
                value => DateTimeOffset.FromUnixTimeMilliseconds(value));

        workoutSession.Property(item => item.FinishedAt)
            .HasConversion(nullableDateTimeOffsetConverter);

        workoutSession.Property(item => item.Notes)
            .HasMaxLength(2000);

        workoutSession.Property(item => item.Status)
            .HasConversion<string>()
            .HasMaxLength(16);

        workoutSession.HasOne(item => item.Routine)
            .WithMany()
            .HasForeignKey(item => item.RoutineId)
            .OnDelete(DeleteBehavior.SetNull);

        workoutSession.HasIndex(item => item.StartedAt);

        workoutSession.HasIndex(item => new { item.Status, item.StartedAt });

        workoutSession.HasIndex(item => item.Status)
            .IsUnique()
            .HasFilter("\"Status\" = 'InProgress'");

        workoutSession.HasIndex(item => new { item.Status, item.FinishedAt });

        var workoutExercise = modelBuilder.Entity<WorkoutExercise>();

        workoutExercise.ToTable("WorkoutExercises", table =>
            table.HasCheckConstraint(
                "CK_WorkoutExercises_Position",
                "Position >= 0"));

        workoutExercise.HasKey(item => item.Id);

        workoutExercise.Property(item => item.ExerciseName)
            .IsRequired()
            .HasMaxLength(100);

        workoutExercise.Property(item => item.Notes)
            .HasMaxLength(2000);

        workoutExercise.HasOne(item => item.WorkoutSession)
            .WithMany(item => item.Exercises)
            .HasForeignKey(item => item.WorkoutSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        workoutExercise.HasOne(item => item.Exercise)
            .WithMany()
            .HasForeignKey(item => item.ExerciseId)
            .OnDelete(DeleteBehavior.SetNull);

        workoutExercise.HasIndex(item => new { item.WorkoutSessionId, item.Position });

        workoutExercise.HasIndex(item => new { item.ExerciseId, item.WorkoutSessionId });

        var workoutSet = modelBuilder.Entity<WorkoutSet>();

        workoutSet.ToTable("WorkoutSets", table =>
        {
            table.HasCheckConstraint("CK_WorkoutSets_SetNumber", "SetNumber > 0");
            table.HasCheckConstraint("CK_WorkoutSets_Weight", "Weight >= 0");
            table.HasCheckConstraint("CK_WorkoutSets_Repetitions", "Repetitions >= 0");
            table.HasCheckConstraint("CK_WorkoutSets_Rpe", "Rpe IS NULL OR Rpe BETWEEN 0 AND 10");
        });

        workoutSet.HasKey(item => item.Id);

        workoutSet.Property(item => item.Weight)
            .HasConversion(
                value => (long)(value * 1000m),
                value => value / 1000m);

        workoutSet.Property(item => item.CompletedAt)
            .HasConversion(nullableDateTimeOffsetConverter);

        workoutSet.Property(item => item.SetType)
            .HasConversion<string>()
            .HasMaxLength(16);

        workoutSet.HasOne(item => item.WorkoutExercise)
            .WithMany(item => item.Sets)
            .HasForeignKey(item => item.WorkoutExerciseId)
            .OnDelete(DeleteBehavior.Cascade);

        workoutSet.HasIndex(item => new { item.WorkoutExerciseId, item.SetNumber });

        workoutSet.HasIndex(item => new { item.WorkoutExerciseId, item.CompletedAt });

        var bodyMeasurement = modelBuilder.Entity<BodyMeasurement>();

        bodyMeasurement.ToTable("BodyMeasurements", table =>
        {
            table.HasCheckConstraint("CK_BodyMeasurements_WeightKg", "WeightKg IS NULL OR WeightKg BETWEEN 1 AND 1000000");
            table.HasCheckConstraint("CK_BodyMeasurements_BodyFatPercentage", "BodyFatPercentage IS NULL OR BodyFatPercentage BETWEEN 0 AND 100000");
            table.HasCheckConstraint("CK_BodyMeasurements_WaistCm", "WaistCm IS NULL OR WaistCm BETWEEN 1 AND 500000");
        });

        bodyMeasurement.HasKey(item => item.Id);
        bodyMeasurement.Property(item => item.RecordedAt)
            .HasConversion(
                value => value.ToUnixTimeMilliseconds(),
                value => DateTimeOffset.FromUnixTimeMilliseconds(value));
        bodyMeasurement.Property(item => item.WeightKg)
            .HasConversion(
                value => value.HasValue ? (long?)(value.Value * 1000m) : null,
                value => value.HasValue ? value.Value / 1000m : null);
        bodyMeasurement.Property(item => item.BodyFatPercentage)
            .HasConversion(
                value => value.HasValue ? (long?)(value.Value * 1000m) : null,
                value => value.HasValue ? value.Value / 1000m : null);
        bodyMeasurement.Property(item => item.WaistCm)
            .HasConversion(
                value => value.HasValue ? (long?)(value.Value * 1000m) : null,
                value => value.HasValue ? value.Value / 1000m : null);
        bodyMeasurement.Property(item => item.Notes).HasMaxLength(500);
        bodyMeasurement.HasIndex(item => item.RecordedAt);
    }
}
