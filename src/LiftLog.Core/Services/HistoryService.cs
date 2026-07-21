using LiftLog.Core.Data;
using LiftLog.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace LiftLog.Core.Services;

public sealed class HistoryService(
    IDbContextFactory<LiftLogDbContext> contextFactory,
    IDatabaseInitializer databaseInitializer) : IHistoryService
{
    public async Task<IReadOnlyList<WorkoutSession>> GetCompletedAsync(
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var query = CreateCompletedQuery(context);

        if (from is not null)
        {
            query = query.Where(session => session.StartedAt >= from.Value);
        }

        if (to is not null)
        {
            query = query.Where(session => session.StartedAt <= to.Value);
        }

        return await query
            .OrderByDescending(session => session.StartedAt)
            .ThenByDescending(session => session.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WorkoutSession>> GetRecentCompletedAsync(
        int maximumCount,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumCount);

        await databaseInitializer.InitializeAsync(cancellationToken);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        return await CreateCompletedQuery(context)
            .OrderByDescending(session => session.StartedAt)
            .ThenByDescending(session => session.Id)
            .Take(maximumCount)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WorkoutSession>> GetCompletedInFinishedRangeAsync(
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive,
        CancellationToken cancellationToken = default)
    {
        if (toExclusive <= fromInclusive)
        {
            throw new ArgumentException(
                "The end of the completed-workout interval must be after its start.",
                nameof(toExclusive));
        }

        await databaseInitializer.InitializeAsync(cancellationToken);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        return await CreateCompletedQuery(context)
            .Where(session =>
                session.FinishedAt != null &&
                session.FinishedAt >= fromInclusive &&
                session.FinishedAt < toExclusive)
            .OrderByDescending(session => session.StartedAt)
            .ThenByDescending(session => session.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WorkoutSession>> GetCompletedPageAsync(
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        if (skip < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(skip));
        }

        if (take <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(take));
        }

        await databaseInitializer.InitializeAsync(cancellationToken);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        return await CreateCompletedQuery(context)
            .OrderByDescending(session => session.StartedAt)
            .ThenByDescending(session => session.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<WorkoutSession?> GetByIdAsync(
        int sessionId,
        CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        return await CreateCompletedQuery(context)
            .SingleOrDefaultAsync(session => session.Id == sessionId, cancellationToken);
    }

    public async Task<WorkoutSession?> GetPreviousCompletedForRoutineAsync(
        int sessionId,
        CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var current = await context.WorkoutSessions
            .AsNoTracking()
            .Where(session => session.Id == sessionId)
            .Select(session => new
            {
                session.Id,
                session.RoutineId,
                session.RoutineName,
                session.StartedAt
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (current is null)
        {
            return null;
        }

        var query = CreateCompletedQuery(context)
            .Where(session =>
                session.Id != current.Id &&
                (session.StartedAt < current.StartedAt ||
                 (session.StartedAt == current.StartedAt && session.Id < current.Id)));

        query = current.RoutineId is { } routineId
            ? query.Where(session => session.RoutineId == routineId)
            : query.Where(session =>
                session.RoutineId == null &&
                session.RoutineName == current.RoutineName);

        return await query
            .OrderByDescending(session => session.StartedAt)
            .ThenByDescending(session => session.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public TimeSpan CalculateDuration(WorkoutSession session)
    {
        if (session.FinishedAt is null || session.FinishedAt <= session.StartedAt)
        {
            return TimeSpan.Zero;
        }

        return session.FinishedAt.Value - session.StartedAt;
    }

    public decimal CalculateVolume(WorkoutSession session) =>
        session.Exercises.Sum(CalculateVolume);

    public decimal CalculateVolume(WorkoutExercise exercise) =>
        exercise.Sets
            .Where(workoutSet => workoutSet.IsCompleted && !workoutSet.IsWarmup)
            .Sum(workoutSet => workoutSet.Weight * workoutSet.Repetitions);

    public int CountCompletedSets(WorkoutSession session) =>
        session.Exercises
            .SelectMany(exercise => exercise.Sets)
            .Count(workoutSet => workoutSet.IsCompleted);

    private static IQueryable<WorkoutSession> CreateCompletedQuery(LiftLogDbContext context) =>
        context.WorkoutSessions
            .AsNoTracking()
            .Where(session => session.Status == WorkoutStatus.Completed)
            .Include(session => session.Exercises.OrderBy(exercise => exercise.Position))
                .ThenInclude(exercise => exercise.Sets.OrderBy(workoutSet => workoutSet.SetNumber));
}
