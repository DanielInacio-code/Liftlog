using LiftLog.Core.Data;
using LiftLog.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace LiftLog.Core.Services;

public sealed class WorkoutService(
    IDbContextFactory<LiftLogDbContext> contextFactory,
    IDatabaseInitializer databaseInitializer) : IWorkoutService
{
    private const decimal MaximumWeight = 10000m;
    private const int MaximumRepetitions = 10000;
    private const int MaximumRestTimerSeconds = 3600;
    private const int MaximumExerciseNotesLength = 2000;

    public event EventHandler<ActiveWorkoutChangedEventArgs>? ActiveWorkoutChanged;

    public async Task<WorkoutSession> StartAsync(
        int routineId,
        CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        if (await context.WorkoutSessions.AnyAsync(
                session => session.Status == WorkoutStatus.InProgress,
                cancellationToken))
        {
            throw new WorkoutValidationException("A workout is already in progress.");
        }

        var routine = await context.Routines
            .AsNoTracking()
            .Include(item => item.Exercises.OrderBy(exercise => exercise.Position))
                .ThenInclude(item => item.Exercise)
            .Include(item => item.Exercises)
                .ThenInclude(item => item.Sets.OrderBy(set => set.SetNumber))
            .SingleOrDefaultAsync(item => item.Id == routineId, cancellationToken)
            ?? throw new WorkoutValidationException("The routine was not found.");

        if (routine.Exercises.Count == 0)
        {
            throw new WorkoutValidationException("Add at least one exercise before starting the workout.");
        }

        var previousSession = await context.WorkoutSessions
            .AsNoTracking()
            .Include(item => item.Exercises)
                .ThenInclude(item => item.Sets.OrderBy(set => set.SetNumber))
            .Where(item => item.RoutineId == routine.Id &&
                           item.Status == WorkoutStatus.Completed)
            .OrderByDescending(item => item.FinishedAt)
            .FirstOrDefaultAsync(cancellationToken);
        var previousExercises = previousSession?.Exercises
            .Where(item => item.ExerciseId.HasValue)
            .ToDictionary(item => item.ExerciseId!.Value)
            ?? new Dictionary<int, WorkoutExercise>();

        var session = new WorkoutSession
        {
            RoutineId = routine.Id,
            RoutineName = routine.Name,
            StartedAt = DateTimeOffset.UtcNow,
            Status = WorkoutStatus.InProgress,
            Exercises = routine.Exercises
                .OrderBy(item => item.Position)
                .Select(item =>
                {
                    var previousSets = previousExercises.TryGetValue(
                        item.ExerciseId,
                        out var previousExercise)
                        ? previousExercise.Sets.OrderBy(set => set.SetNumber).ToArray()
                        : [];

                    return new WorkoutExercise
                    {
                        ExerciseId = item.ExerciseId,
                        ExerciseName = item.Exercise.Name,
                        Position = item.Position,
                        Sets = item.Sets
                            .OrderBy(set => set.SetNumber)
                            .Select((set, index) =>
                            {
                                var previousSet = index < previousSets.Length &&
                                                  previousSets[index].IsCompleted
                                    ? previousSets[index]
                                    : null;
                                return new WorkoutSet
                                {
                                    SetNumber = set.SetNumber,
                                    Weight = previousSet?.Weight ?? set.Weight,
                                    Repetitions = previousSet?.Repetitions ?? set.Repetitions,
                                    IsWarmup = set.IsWarmup,
                                    SetType = set.SetType,
                                    Rpe = null
                                };
                            })
                            .ToList()
                    };
                })
                .ToList()
        };

        context.WorkoutSessions.Add(session);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            throw new WorkoutValidationException(
                "The workout could not be started. Check whether another workout is already in progress.",
                exception);
        }

        NotifyActiveWorkoutChanged(session);
        return session;
    }

    public async Task<WorkoutSession?> GetActiveAsync(
        CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        return await CreateSessionQuery(context)
            .SingleOrDefaultAsync(
                session => session.Status == WorkoutStatus.InProgress,
                cancellationToken);
    }

    public async Task<ActiveWorkoutOverview?> GetActiveOverviewAsync(
        CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.WorkoutSessions
            .AsNoTracking()
            .Where(session => session.Status == WorkoutStatus.InProgress)
            .Select(session => new ActiveWorkoutOverview(
                session.StartedAt,
                session.RoutineName,
                session.Exercises
                    .Where(exercise =>
                        !exercise.Sets.Any() ||
                        exercise.Sets.Any(workoutSet => !workoutSet.IsCompleted))
                    .OrderBy(exercise => exercise.Position)
                    .Select(exercise => exercise.ExerciseName)
                    .FirstOrDefault() ??
                session.Exercises
                    .OrderByDescending(exercise => exercise.Position)
                    .Select(exercise => exercise.ExerciseName)
                    .FirstOrDefault()))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> HasActiveAsync(CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.WorkoutSessions
            .AsNoTracking()
            .AnyAsync(
                session => session.Status == WorkoutStatus.InProgress,
                cancellationToken);
    }

    public async Task<WorkoutExercise> AddExerciseAsync(
        int sessionId,
        int exerciseId,
        CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var session = await context.WorkoutSessions
            .Include(item => item.Exercises)
            .SingleOrDefaultAsync(item => item.Id == sessionId, cancellationToken)
            ?? throw new WorkoutValidationException("The workout was not found.");
        EnsureWorkoutIsActive(session);

        if (session.Exercises.Any(item => item.ExerciseId == exerciseId))
        {
            throw new WorkoutValidationException("This exercise is already in the workout.");
        }

        var exercise = await context.Exercises
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == exerciseId, cancellationToken)
            ?? throw new WorkoutValidationException("The exercise was not found.");

        var workoutExercise = new WorkoutExercise
        {
            WorkoutSessionId = session.Id,
            ExerciseId = exercise.Id,
            ExerciseName = exercise.Name,
            Position = session.Exercises.Count,
            Sets =
            [
                new WorkoutSet { SetNumber = 1 }
            ]
        };

        context.WorkoutExercises.Add(workoutExercise);
        await context.SaveChangesAsync(cancellationToken);
        return await GetWorkoutExerciseByIdAsync(workoutExercise.Id, cancellationToken);
    }

    public async Task<WorkoutExercise> ReplaceExerciseAsync(
        int workoutExerciseId,
        int exerciseId,
        CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var workoutExercise = await context.WorkoutExercises
            .Include(item => item.WorkoutSession)
                .ThenInclude(item => item.Exercises)
            .SingleOrDefaultAsync(item => item.Id == workoutExerciseId, cancellationToken)
            ?? throw new WorkoutValidationException("The workout exercise was not found.");
        EnsureWorkoutIsActive(workoutExercise.WorkoutSession);

        if (workoutExercise.WorkoutSession.Exercises.Any(
                item => item.Id != workoutExerciseId && item.ExerciseId == exerciseId))
        {
            throw new WorkoutValidationException("This exercise is already in the workout.");
        }

        var replacement = await context.Exercises
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == exerciseId, cancellationToken)
            ?? throw new WorkoutValidationException("The exercise was not found.");

        workoutExercise.ExerciseId = replacement.Id;
        workoutExercise.ExerciseName = replacement.Name;
        workoutExercise.RestTimerSeconds = 0;
        await context.SaveChangesAsync(cancellationToken);
        return await GetWorkoutExerciseByIdAsync(workoutExercise.Id, cancellationToken);
    }

    public async Task MoveExerciseAsync(
        int workoutExerciseId,
        int newPosition,
        CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var workoutExercise = await context.WorkoutExercises
            .Include(item => item.WorkoutSession)
                .ThenInclude(item => item.Exercises)
            .SingleOrDefaultAsync(item => item.Id == workoutExerciseId, cancellationToken)
            ?? throw new WorkoutValidationException("The workout exercise was not found.");
        EnsureWorkoutIsActive(workoutExercise.WorkoutSession);

        var ordered = workoutExercise.WorkoutSession.Exercises
            .OrderBy(item => item.Position)
            .ToList();
        if (newPosition < 0 || newPosition >= ordered.Count)
        {
            return;
        }

        ordered.Remove(workoutExercise);
        ordered.Insert(newPosition, workoutExercise);
        for (var position = 0; position < ordered.Count; position++)
        {
            ordered[position].Position = position;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveExerciseAsync(
        int workoutExerciseId,
        CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var workoutExercise = await context.WorkoutExercises
            .Include(item => item.WorkoutSession)
                .ThenInclude(item => item.Exercises)
            .SingleOrDefaultAsync(item => item.Id == workoutExerciseId, cancellationToken)
            ?? throw new WorkoutValidationException("The workout exercise was not found.");
        EnsureWorkoutIsActive(workoutExercise.WorkoutSession);

        var remaining = workoutExercise.WorkoutSession.Exercises
            .Where(item => item.Id != workoutExerciseId)
            .OrderBy(item => item.Position)
            .ToList();
        context.WorkoutExercises.Remove(workoutExercise);
        for (var position = 0; position < remaining.Count; position++)
        {
            remaining[position].Position = position;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<WorkoutExercise> SetRestTimerAsync(
        int workoutExerciseId,
        int seconds,
        CancellationToken cancellationToken = default)
    {
        if (seconds < 0 || seconds > MaximumRestTimerSeconds)
        {
            throw new WorkoutValidationException(
                $"The rest timer must be between 0 and {MaximumRestTimerSeconds} seconds.");
        }

        await databaseInitializer.InitializeAsync(cancellationToken);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var workoutExercise = await context.WorkoutExercises
            .Include(item => item.WorkoutSession)
            .SingleOrDefaultAsync(item => item.Id == workoutExerciseId, cancellationToken)
            ?? throw new WorkoutValidationException("The workout exercise was not found.");
        EnsureWorkoutIsActive(workoutExercise.WorkoutSession);

        workoutExercise.RestTimerSeconds = seconds;
        await context.SaveChangesAsync(cancellationToken);
        return await GetWorkoutExerciseByIdAsync(workoutExercise.Id, cancellationToken);
    }

    public async Task<WorkoutExercise> UpdateExerciseNotesAsync(
        int workoutExerciseId,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        if (notes?.Length > MaximumExerciseNotesLength)
        {
            throw new WorkoutValidationException(
                $"Exercise notes cannot exceed {MaximumExerciseNotesLength:N0} characters.");
        }

        await databaseInitializer.InitializeAsync(cancellationToken);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var workoutExercise = await context.WorkoutExercises
            .Include(item => item.WorkoutSession)
            .SingleOrDefaultAsync(item => item.Id == workoutExerciseId, cancellationToken)
            ?? throw new WorkoutValidationException("The workout exercise was not found.");
        EnsureWorkoutIsActive(workoutExercise.WorkoutSession);

        workoutExercise.Notes = notes;
        await context.SaveChangesAsync(cancellationToken);
        return await GetWorkoutExerciseByIdAsync(workoutExercise.Id, cancellationToken);
    }

    public async Task<WorkoutSet> AddSetAsync(
        int workoutExerciseId,
        CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var exercise = await context.WorkoutExercises
            .Include(item => item.WorkoutSession)
            .Include(item => item.Sets)
            .SingleOrDefaultAsync(item => item.Id == workoutExerciseId, cancellationToken)
            ?? throw new WorkoutValidationException("The workout exercise was not found.");

        EnsureWorkoutIsActive(exercise.WorkoutSession);

        var workoutSet = new WorkoutSet
        {
            WorkoutExerciseId = exercise.Id,
            SetNumber = exercise.Sets.Count == 0
                ? 1
                : exercise.Sets.Max(item => item.SetNumber) + 1
        };

        context.WorkoutSets.Add(workoutSet);
        await context.SaveChangesAsync(cancellationToken);
        return workoutSet;
    }

    public async Task<WorkoutSet> UpdateSetAsync(
        int setId,
        WorkoutSetInput input,
        CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);
        var normalized = NormalizeAndValidate(input);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var workoutSet = await GetTrackedSetAsync(context, setId, cancellationToken);
        EnsureWorkoutIsActive(workoutSet.WorkoutExercise.WorkoutSession);

        ApplySetInput(workoutSet, normalized);

        await context.SaveChangesAsync(cancellationToken);
        return workoutSet;
    }

    public async Task<WorkoutSet> UpdateSetAndCompletionAsync(
        int setId,
        WorkoutSetInput input,
        bool isCompleted,
        CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);
        var normalized = NormalizeAndValidate(input);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var workoutSet = await GetTrackedSetAsync(context, setId, cancellationToken);
        EnsureWorkoutIsActive(workoutSet.WorkoutExercise.WorkoutSession);

        ApplySetInput(workoutSet, normalized);
        workoutSet.IsCompleted = isCompleted;
        workoutSet.CompletedAt = isCompleted ? DateTimeOffset.UtcNow : null;

        await context.SaveChangesAsync(cancellationToken);
        return workoutSet;
    }

    public async Task<WorkoutSet> SetCompletedAsync(
        int setId,
        bool isCompleted,
        CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var workoutSet = await GetTrackedSetAsync(context, setId, cancellationToken);
        EnsureWorkoutIsActive(workoutSet.WorkoutExercise.WorkoutSession);

        workoutSet.IsCompleted = isCompleted;
        workoutSet.CompletedAt = isCompleted ? DateTimeOffset.UtcNow : null;

        await context.SaveChangesAsync(cancellationToken);
        return workoutSet;
    }

    public async Task DeleteSetAsync(
        int setId,
        CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var workoutSet = await GetTrackedSetAsync(context, setId, cancellationToken);
        EnsureWorkoutIsActive(workoutSet.WorkoutExercise.WorkoutSession);

        var exerciseId = workoutSet.WorkoutExerciseId;
        var remaining = await context.WorkoutSets
            .Where(item => item.WorkoutExerciseId == exerciseId && item.Id != setId)
            .OrderBy(item => item.SetNumber)
            .ToListAsync(cancellationToken);

        context.WorkoutSets.Remove(workoutSet);

        for (var index = 0; index < remaining.Count; index++)
        {
            remaining[index].SetNumber = index + 1;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<WorkoutSession> CompleteAsync(
        int sessionId,
        CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var session = await context.WorkoutSessions
            .Include(item => item.Exercises.OrderBy(exercise => exercise.Position))
                .ThenInclude(item => item.Sets.OrderBy(set => set.SetNumber))
            .SingleOrDefaultAsync(item => item.Id == sessionId, cancellationToken)
            ?? throw new WorkoutValidationException("The workout was not found.");

        EnsureWorkoutIsActive(session);

        if (!session.Exercises.SelectMany(item => item.Sets).Any(item => item.IsCompleted))
        {
            throw new WorkoutValidationException("Complete at least one set before finishing the workout.");
        }

        session.Status = WorkoutStatus.Completed;
        session.FinishedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        NotifyActiveWorkoutChanged(null);
        return session;
    }

    public async Task<WorkoutSession> CancelAsync(
        int sessionId,
        CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var session = await context.WorkoutSessions
            .Include(item => item.Exercises.OrderBy(exercise => exercise.Position))
                .ThenInclude(item => item.Sets.OrderBy(set => set.SetNumber))
            .SingleOrDefaultAsync(item => item.Id == sessionId, cancellationToken)
            ?? throw new WorkoutValidationException("The workout was not found.");

        EnsureWorkoutIsActive(session);
        session.Status = WorkoutStatus.Cancelled;
        session.FinishedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        NotifyActiveWorkoutChanged(null);
        return session;
    }

    public async Task<PreviousExercisePerformance?> GetPreviousPerformanceAsync(
        int exerciseId,
        int currentSessionId,
        CancellationToken cancellationToken = default)
    {
        var performances = await GetPreviousPerformancesAsync(
            [exerciseId],
            currentSessionId,
            cancellationToken);
        return performances.GetValueOrDefault(exerciseId);
    }

    public async Task<IReadOnlyDictionary<int, PreviousExercisePerformance>> GetPreviousPerformancesAsync(
        IReadOnlyCollection<int> exerciseIds,
        int currentSessionId,
        CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);
        var ids = exerciseIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<int, PreviousExercisePerformance>();
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var currentRoutineId = await context.WorkoutSessions
            .Where(session => session.Id == currentSessionId)
            .Select(session => session.RoutineId)
            .SingleOrDefaultAsync(cancellationToken);

        var candidates = await context.WorkoutExercises
            .AsNoTracking()
            .Include(item => item.WorkoutSession)
            .Include(item => item.Sets.OrderBy(set => set.SetNumber))
            .Where(item => item.ExerciseId != null &&
                           ids.Contains(item.ExerciseId.Value) &&
                           item.WorkoutSessionId != currentSessionId &&
                           item.WorkoutSession.RoutineId == currentRoutineId &&
                           item.WorkoutSession.Status == WorkoutStatus.Completed)
            .OrderByDescending(item => item.WorkoutSession.FinishedAt)
            .ToListAsync(cancellationToken);

        return candidates
            .Where(item => item.WorkoutSession.FinishedAt is not null)
            .GroupBy(item => item.ExerciseId!.Value)
            .ToDictionary(
                group => group.Key,
                group => CreatePreviousPerformance(group.First()));
    }

    public TimeSpan CalculateDuration(WorkoutSession session, DateTimeOffset? now = null)
    {
        var end = session.FinishedAt ?? now ?? DateTimeOffset.UtcNow;
        return end <= session.StartedAt ? TimeSpan.Zero : end - session.StartedAt;
    }

    private void NotifyActiveWorkoutChanged(WorkoutSession? workout)
    {
        var handlers = ActiveWorkoutChanged;
        if (handlers is null)
        {
            return;
        }

        var eventArgs = new ActiveWorkoutChangedEventArgs(workout);
        foreach (EventHandler<ActiveWorkoutChangedEventArgs> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(this, eventArgs);
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine(exception);
            }
        }
    }

    private async Task<WorkoutExercise> GetWorkoutExerciseByIdAsync(
        int workoutExerciseId,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.WorkoutExercises
            .AsNoTracking()
            .Include(item => item.Sets.OrderBy(set => set.SetNumber))
            .SingleAsync(item => item.Id == workoutExerciseId, cancellationToken);
    }

    private static IQueryable<WorkoutSession> CreateSessionQuery(LiftLogDbContext context) =>
        context.WorkoutSessions
            .AsNoTracking()
            .Include(session => session.Exercises.OrderBy(exercise => exercise.Position))
                .ThenInclude(exercise => exercise.Sets.OrderBy(set => set.SetNumber));

    private static async Task<WorkoutSet> GetTrackedSetAsync(
        LiftLogDbContext context,
        int setId,
        CancellationToken cancellationToken) =>
        await context.WorkoutSets
            .Include(item => item.WorkoutExercise)
                .ThenInclude(item => item.WorkoutSession)
            .SingleOrDefaultAsync(item => item.Id == setId, cancellationToken)
        ?? throw new WorkoutValidationException("The set was not found.");

    private static WorkoutSetInput NormalizeAndValidate(WorkoutSetInput input)
    {
        if (input.Weight is < 0 or > MaximumWeight)
        {
            throw new WorkoutValidationException($"Weight must be between 0 and {MaximumWeight} kg.");
        }

        if (input.Repetitions is < 0 or > MaximumRepetitions)
        {
            throw new WorkoutValidationException(
                $"Repetitions must be between 0 and {MaximumRepetitions}.");
        }

        if (input.Rpe is { } rpe && (!double.IsFinite(rpe) || rpe is < 0 or > 10))
        {
            throw new WorkoutValidationException("RPE must be between 0 and 10.");
        }

        var setType = input.IsWarmup ? TrainingSetType.Warmup : input.SetType;
        if (!Enum.IsDefined(setType))
        {
            throw new WorkoutValidationException("Select a valid set type.");
        }

        return input with
        {
            Weight = decimal.Round(input.Weight, 3, MidpointRounding.AwayFromZero),
            IsWarmup = setType == TrainingSetType.Warmup,
            SetType = setType
        };
    }

    private static void ApplySetInput(WorkoutSet workoutSet, WorkoutSetInput input)
    {
        workoutSet.Weight = input.Weight;
        workoutSet.Repetitions = input.Repetitions;
        workoutSet.IsWarmup = input.IsWarmup;
        workoutSet.SetType = input.SetType;
        workoutSet.Rpe = input.Rpe;
    }

    private static void EnsureWorkoutIsActive(WorkoutSession session)
    {
        if (session.Status != WorkoutStatus.InProgress)
        {
            throw new WorkoutValidationException("This workout is no longer in progress.");
        }
    }

    private static PreviousExercisePerformance CreatePreviousPerformance(
        WorkoutExercise previous)
    {
        var allSets = previous.Sets
            .Where(item => item.IsCompleted)
            .OrderBy(item => item.SetNumber)
            .Select(item => new PreviousSetPerformance(
                item.Weight,
                item.Repetitions,
                item.IsWarmup,
                item.SetNumber,
                item.Rpe))
            .ToList();
        var workingSets = allSets
            .Where(item => !item.IsWarmup)
            .ToList();

        return new PreviousExercisePerformance(
            previous.WorkoutSession.FinishedAt!.Value,
            workingSets,
            allSets);
    }
}
