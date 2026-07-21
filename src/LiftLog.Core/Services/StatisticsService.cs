using LiftLog.Core.Data;
using LiftLog.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace LiftLog.Core.Services;

public sealed class StatisticsService(
    IDbContextFactory<LiftLogDbContext> contextFactory,
    IDatabaseInitializer databaseInitializer) : IStatisticsService
{
    public async Task<ExerciseProgress> GetExerciseProgressAsync(
        int exerciseId,
        CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var exerciseName = await context.Exercises
            .AsNoTracking()
            .Where(exercise => exercise.Id == exerciseId)
            .Select(exercise => exercise.Name)
            .SingleOrDefaultAsync(cancellationToken);

        var workoutExercises = await context.WorkoutExercises
            .AsNoTracking()
            .Include(exercise => exercise.WorkoutSession)
            .Include(exercise => exercise.Sets.OrderBy(workoutSet => workoutSet.SetNumber))
            .Where(exercise => exercise.ExerciseId == exerciseId &&
                               exercise.WorkoutSession.Status == WorkoutStatus.Completed)
            .OrderBy(exercise => exercise.WorkoutSession.StartedAt)
            .ToListAsync(cancellationToken);

        exerciseName ??= workoutExercises.LastOrDefault()?.ExerciseName ?? string.Empty;

        var points = workoutExercises
            .Select(exercise =>
            {
                var validSets = GetValidSets(exercise.Sets).ToList();
                return validSets.Count == 0
                    ? null
                    : new ExerciseProgressPoint(
                        exercise.WorkoutSessionId,
                        exercise.WorkoutSession.StartedAt,
                        validSets.Max(workoutSet => workoutSet.Weight),
                        validSets.Max(workoutSet => workoutSet.Repetitions),
                        validSets.Sum(workoutSet => workoutSet.Weight * workoutSet.Repetitions));
            })
            .Where(point => point is not null)
            .Cast<ExerciseProgressPoint>()
            .ToList();

        return new ExerciseProgress(
            exerciseId,
            exerciseName,
            points.Count == 0 ? 0 : points.Max(point => point.MaximumWeight),
            points.Count == 0 ? 0 : points.Max(point => point.BestRepetitions),
            points.Sum(point => point.Volume),
            points.Count,
            points);
    }

    public async Task<bool> IsWeightPersonalRecordAsync(
        int workoutSetId,
        CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        return await CreateWeightRecordQuery(context)
            .AnyAsync(workoutSet => workoutSet.Id == workoutSetId, cancellationToken);
    }

    public async Task<IReadOnlySet<int>> GetWeightPersonalRecordSetIdsAsync(
        int workoutSessionId,
        CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var recordSetIds = await CreateWeightRecordQuery(context)
            .Where(workoutSet => workoutSet.WorkoutExercise.WorkoutSessionId == workoutSessionId)
            .Select(workoutSet => workoutSet.Id)
            .ToListAsync(cancellationToken);

        return recordSetIds.ToHashSet();
    }

    public async Task<IReadOnlySet<int>> GetWeightPersonalRecordSetIdsAsync(
        int workoutSessionId,
        int exerciseId,
        CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var recordSetIds = await CreateWeightRecordQuery(context)
            .Where(workoutSet =>
                workoutSet.WorkoutExercise.WorkoutSessionId == workoutSessionId &&
                workoutSet.WorkoutExercise.ExerciseId == exerciseId)
            .Select(workoutSet => workoutSet.Id)
            .ToListAsync(cancellationToken);

        return recordSetIds.ToHashSet();
    }

    public decimal CalculateVolume(IEnumerable<WorkoutSet> sets) =>
        GetValidSets(sets).Sum(workoutSet => workoutSet.Weight * workoutSet.Repetitions);

    public decimal CalculateMaximumWeight(IEnumerable<WorkoutSet> sets)
    {
        var validSets = GetValidSets(sets).ToList();
        return validSets.Count == 0 ? 0 : validSets.Max(workoutSet => workoutSet.Weight);
    }

    public int CalculateBestRepetitions(IEnumerable<WorkoutSet> sets)
    {
        var validSets = GetValidSets(sets).ToList();
        return validSets.Count == 0 ? 0 : validSets.Max(workoutSet => workoutSet.Repetitions);
    }

    private static IQueryable<WorkoutSet> CreateRecordCandidateQuery(LiftLogDbContext context) =>
        context.WorkoutSets
            .AsNoTracking()
            .Where(workoutSet => workoutSet.IsCompleted &&
                                 !workoutSet.IsWarmup &&
                                 workoutSet.CompletedAt != null &&
                                 workoutSet.WorkoutExercise.ExerciseId != null &&
                                 workoutSet.WorkoutExercise.WorkoutSession.Status != WorkoutStatus.Cancelled);

    private static IQueryable<WorkoutSet> CreateWeightRecordQuery(LiftLogDbContext context)
    {
        var candidates = CreateRecordCandidateQuery(context);
        return candidates.Where(current =>
            current.Weight > 0 &&
            !candidates.Any(previous =>
                previous.Id != current.Id &&
                previous.WorkoutExercise.ExerciseId == current.WorkoutExercise.ExerciseId &&
                previous.WorkoutExercise.WorkoutSession.RoutineId ==
                    current.WorkoutExercise.WorkoutSession.RoutineId &&
                (current.WorkoutExercise.WorkoutSession.RoutineId != null ||
                 previous.WorkoutExercise.WorkoutSession.RoutineName ==
                    current.WorkoutExercise.WorkoutSession.RoutineName) &&
                previous.Weight >= current.Weight &&
                (previous.CompletedAt < current.CompletedAt ||
                 (previous.CompletedAt == current.CompletedAt && previous.Id < current.Id))));
    }

    private static IEnumerable<WorkoutSet> GetValidSets(IEnumerable<WorkoutSet> sets) =>
        sets.Where(workoutSet => workoutSet.IsCompleted && !workoutSet.IsWarmup);
}
