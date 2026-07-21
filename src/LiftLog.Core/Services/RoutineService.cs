using LiftLog.Core.Data;
using LiftLog.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace LiftLog.Core.Services;

public sealed class RoutineService(
    IDbContextFactory<LiftLogDbContext> contextFactory,
    IDatabaseInitializer databaseInitializer) : IRoutineService
{
    private const int MaximumExercises = 50;
    private const int MaximumSetsPerExercise = 20;
    private const decimal MaximumWeight = 10000m;
    private const int MaximumRepetitions = 10000;

    public async Task<IReadOnlyList<Routine>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.Routines
            .AsNoTracking()
            .Include(routine => routine.Exercises.OrderBy(item => item.Position))
                .ThenInclude(item => item.Exercise)
            .OrderByDescending(routine => routine.UpdatedAt)
            .ThenBy(routine => routine.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Routine?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.Routines
            .AsNoTracking()
            .Include(routine => routine.Exercises.OrderBy(item => item.Position))
                .ThenInclude(item => item.Exercise)
            .Include(routine => routine.Exercises)
                .ThenInclude(item => item.Sets.OrderBy(set => set.SetNumber))
            .SingleOrDefaultAsync(routine => routine.Id == id, cancellationToken);
    }

    public async Task<Routine> CreateAsync(
        RoutineInput input,
        CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);
        var normalized = NormalizeAndValidate(input);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureNameIsUniqueAsync(context, normalized.Name, null, cancellationToken);
        await EnsureExercisesExistAsync(context, normalized.ExerciseIds, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var routine = new Routine
        {
            Name = normalized.Name,
            NormalizedName = NormalizeName(normalized.Name),
            CreatedAt = now,
            UpdatedAt = now,
            Exercises = normalized.ExercisePlans!
                .Select((plan, position) => new RoutineExercise
                {
                    ExerciseId = plan.ExerciseId,
                    Position = position,
                    Sets = CreateRoutineSets(plan.Sets)
                })
                .ToList()
        };

        context.Routines.Add(routine);
        await SaveChangesAsync(context, cancellationToken);

        return await GetByIdAsync(routine.Id, cancellationToken)
            ?? throw new InvalidOperationException("The saved routine could not be loaded.");
    }

    public async Task<Routine> UpdateAsync(
        int id,
        RoutineInput input,
        CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);
        var normalized = NormalizeAndValidate(input);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var routine = await context.Routines
            .Include(item => item.Exercises)
                .ThenInclude(item => item.Sets)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new RoutineValidationException("The routine was not found.");

        await EnsureNameIsUniqueAsync(context, normalized.Name, id, cancellationToken);
        await EnsureExercisesExistAsync(context, normalized.ExerciseIds, cancellationToken);

        routine.Name = normalized.Name;
        routine.NormalizedName = NormalizeName(normalized.Name);
        routine.UpdatedAt = DateTimeOffset.UtcNow;

        var plans = normalized.ExercisePlans!;
        var desiredExerciseIds = plans.Select(item => item.ExerciseId).ToHashSet();
        var removed = routine.Exercises
            .Where(item => !desiredExerciseIds.Contains(item.ExerciseId))
            .ToList();

        context.RoutineExercises.RemoveRange(removed);

        var existing = routine.Exercises
            .Where(item => desiredExerciseIds.Contains(item.ExerciseId))
            .ToDictionary(item => item.ExerciseId);

        for (var position = 0; position < plans.Count; position++)
        {
            var plan = plans[position];
            var exerciseId = plan.ExerciseId;
            if (existing.TryGetValue(exerciseId, out var item))
            {
                item.Position = position;
                context.RoutineSets.RemoveRange(item.Sets);
                item.Sets.Clear();
                foreach (var routineSet in CreateRoutineSets(plan.Sets))
                {
                    item.Sets.Add(routineSet);
                }
            }
            else
            {
                routine.Exercises.Add(new RoutineExercise
                {
                    ExerciseId = exerciseId,
                    Position = position,
                    Sets = CreateRoutineSets(plan.Sets)
                });
            }
        }

        await SaveChangesAsync(context, cancellationToken);

        return await GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("The saved routine could not be loaded.");
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var routine = await context.Routines
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new RoutineValidationException("The routine was not found.");

        context.Routines.Remove(routine);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static RoutineInput NormalizeAndValidate(RoutineInput input)
    {
        var name = input.Name?.Trim() ?? string.Empty;
        var plans = input.ExercisePlans ?? input.ExerciseIds
            .Select(exerciseId => new RoutineExerciseInput(exerciseId, []))
            .ToArray();
        var exerciseIds = plans.Select(item => item.ExerciseId).ToArray();

        if (name.Length is < 2 or > 100)
        {
            throw new RoutineValidationException("The name must be between 2 and 100 characters.");
        }

        if (plans.Count > MaximumExercises)
        {
            throw new RoutineValidationException($"A routine can contain at most {MaximumExercises} exercises.");
        }

        if (exerciseIds.Distinct().Count() != exerciseIds.Length)
        {
            throw new RoutineValidationException("The same exercise cannot be added more than once.");
        }

        var normalizedPlans = plans.Select(plan =>
        {
            if (plan.Sets.Count > MaximumSetsPerExercise)
            {
                throw new RoutineValidationException(
                    $"An exercise can contain at most {MaximumSetsPerExercise} sets.");
            }

            var sets = plan.Sets.Select(set =>
            {
                if (set.Weight is < 0 or > MaximumWeight)
                {
                    throw new RoutineValidationException(
                        $"Weight must be between 0 and {MaximumWeight} kg.");
                }

                if (set.Repetitions is < 0 or > MaximumRepetitions)
                {
                    throw new RoutineValidationException(
                        $"Repetitions must be between 0 and {MaximumRepetitions}.");
                }

                if (set.Rpe is { } rpe && (!double.IsFinite(rpe) || rpe is < 0 or > 10))
                {
                    throw new RoutineValidationException("RPE must be between 0 and 10.");
                }

                var setType = set.IsWarmup ? TrainingSetType.Warmup : set.SetType;
                if (!Enum.IsDefined(setType))
                {
                    throw new RoutineValidationException("Select a valid set type.");
                }

                return set with
                {
                    Weight = decimal.Round(set.Weight, 3, MidpointRounding.AwayFromZero),
                    IsWarmup = setType == TrainingSetType.Warmup,
                    SetType = setType
                };
            }).ToArray();

            return plan with { Sets = sets };
        }).ToArray();

        return new RoutineInput(name, exerciseIds, normalizedPlans);
    }

    private static List<RoutineSet> CreateRoutineSets(IReadOnlyList<RoutineSetInput> sets) =>
        sets.Select((set, index) => new RoutineSet
        {
            SetNumber = index + 1,
            Weight = set.Weight,
            Repetitions = set.Repetitions,
            IsWarmup = set.IsWarmup,
            SetType = set.SetType,
            Rpe = set.Rpe
        }).ToList();

    private static async Task EnsureNameIsUniqueAsync(
        LiftLogDbContext context,
        string name,
        int? excludedId,
        CancellationToken cancellationToken)
    {
        var normalizedName = NormalizeName(name);
        var duplicateExists = await context.Routines.AnyAsync(
            routine => routine.NormalizedName == normalizedName &&
                       (!excludedId.HasValue || routine.Id != excludedId.Value),
            cancellationToken);

        if (duplicateExists)
        {
            throw new RoutineValidationException("A routine with this name already exists.");
        }
    }

    private static async Task EnsureExercisesExistAsync(
        LiftLogDbContext context,
        IReadOnlyList<int> exerciseIds,
        CancellationToken cancellationToken)
    {
        if (exerciseIds.Count == 0)
        {
            return;
        }

        var existingCount = await context.Exercises
            .CountAsync(exercise => exerciseIds.Contains(exercise.Id), cancellationToken);

        if (existingCount != exerciseIds.Count)
        {
            throw new RoutineValidationException("One or more selected exercises no longer exist.");
        }
    }

    private static string NormalizeName(string name) => name.ToUpperInvariant();

    private static async Task SaveChangesAsync(
        LiftLogDbContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            throw new RoutineValidationException(
                "The routine could not be saved. Check the information you entered.",
                exception);
        }
    }
}
