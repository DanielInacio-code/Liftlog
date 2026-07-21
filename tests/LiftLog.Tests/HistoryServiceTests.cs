using LiftLog.Core.Models;
using LiftLog.Core.Services;
using LiftLog.Tests.Support;

namespace LiftLog.Tests;

public class HistoryServiceTests
{
    [Fact]
    public async Task GetCompletedWorkouts_ExcludesCancelledAndActiveSessions()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var routine = await CreateRoutineAsync(database);

        var completed = await StartWorkoutWithCompletedSetAsync(database, routine.Id, 50m, 10);
        await database.WorkoutService.CompleteAsync(completed.Id);

        var cancelled = await database.WorkoutService.StartAsync(routine.Id);
        await database.WorkoutService.CancelAsync(cancelled.Id);

        await database.WorkoutService.StartAsync(routine.Id);

        var history = await database.HistoryService.GetCompletedAsync();

        var saved = Assert.Single(history);
        Assert.Equal(completed.Id, saved.Id);
        Assert.Equal(WorkoutStatus.Completed, saved.Status);
    }

    [Fact]
    public async Task GetCompletedWorkouts_ReturnsMostRecentFirst()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var routine = await CreateRoutineAsync(database);

        var first = await StartWorkoutWithCompletedSetAsync(database, routine.Id, 40m, 8);
        await database.WorkoutService.CompleteAsync(first.Id);
        var second = await StartWorkoutWithCompletedSetAsync(database, routine.Id, 45m, 8);
        await database.WorkoutService.CompleteAsync(second.Id);

        var history = await database.HistoryService.GetCompletedAsync();

        Assert.Equal([second.Id, first.Id], history.Select(item => item.Id));
    }

    [Fact]
    public async Task CompletedHistoryQueries_WithMatchingStartTimes_UseNewestIdAsTieBreaker()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var routine = await CreateRoutineAsync(database);

        var first = await StartWorkoutWithCompletedSetAsync(database, routine.Id, 40m, 8);
        await database.WorkoutService.CompleteAsync(first.Id);
        var second = await StartWorkoutWithCompletedSetAsync(database, routine.Id, 45m, 8);
        await database.WorkoutService.CompleteAsync(second.Id);

        await using (var context = await database.CreateDbContextAsync())
        {
            var firstSession = await context.WorkoutSessions.FindAsync(first.Id)
                ?? throw new InvalidOperationException("The first workout was not persisted.");
            var secondSession = await context.WorkoutSessions.FindAsync(second.Id)
                ?? throw new InvalidOperationException("The second workout was not persisted.");
            var matchingStartedAt = DateTimeOffset.UnixEpoch;
            firstSession.StartedAt = matchingStartedAt;
            secondSession.StartedAt = matchingStartedAt;
            await context.SaveChangesAsync();
        }

        var all = await database.HistoryService.GetCompletedAsync();
        var recent = await database.HistoryService.GetRecentCompletedAsync(2);
        var page = await database.HistoryService.GetCompletedPageAsync(0, 2);

        var expectedIds = new[] { second.Id, first.Id };
        Assert.Equal(expectedIds, all.Select(item => item.Id));
        Assert.Equal(expectedIds, recent.Select(item => item.Id));
        Assert.Equal(expectedIds, page.Select(item => item.Id));
    }

    [Fact]
    public async Task GetRecentCompletedWorkouts_LimitsTheOrderedResult()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var routine = await CreateRoutineAsync(database);

        var first = await StartWorkoutWithCompletedSetAsync(database, routine.Id, 40m, 8);
        await database.WorkoutService.CompleteAsync(first.Id);
        var second = await StartWorkoutWithCompletedSetAsync(database, routine.Id, 45m, 8);
        await database.WorkoutService.CompleteAsync(second.Id);

        var recent = await database.HistoryService.GetRecentCompletedAsync(1);

        Assert.Equal(second.Id, Assert.Single(recent).Id);
    }

    [Fact]
    public async Task GetCompletedPage_AppliesSkipAndTakeBeforeLoadingGraphs()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var routine = await CreateRoutineAsync(database);

        var first = await StartWorkoutWithCompletedSetAsync(database, routine.Id, 40m, 8);
        await database.WorkoutService.CompleteAsync(first.Id);
        var second = await StartWorkoutWithCompletedSetAsync(database, routine.Id, 45m, 8);
        await database.WorkoutService.CompleteAsync(second.Id);

        var firstPage = await database.HistoryService.GetCompletedPageAsync(0, 1);
        var secondPage = await database.HistoryService.GetCompletedPageAsync(1, 1);

        Assert.Equal(second.Id, Assert.Single(firstPage).Id);
        Assert.Equal(first.Id, Assert.Single(secondPage).Id);
        Assert.NotEmpty(firstPage[0].Exercises);
    }

    [Fact]
    public async Task GetCompletedWorkouts_WithFinishedRange_UsesHalfOpenInterval()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var routine = await CreateRoutineAsync(database);
        var session = await StartWorkoutWithCompletedSetAsync(database, routine.Id, 20m, 10);
        var completed = await database.WorkoutService.CompleteAsync(session.Id);
        var finishedAt = Assert.IsType<DateTimeOffset>(completed.FinishedAt);

        var included = await database.HistoryService.GetCompletedInFinishedRangeAsync(
            finishedAt,
            finishedAt.AddMinutes(1));
        var excludedAtUpperBoundary = await database.HistoryService.GetCompletedInFinishedRangeAsync(
            finishedAt.AddMinutes(-1),
            finishedAt);

        Assert.Equal(completed.Id, Assert.Single(included).Id);
        Assert.Empty(excludedAtUpperBoundary);
    }

    [Fact]
    public async Task GetWorkoutById_LoadsExercisesAndOrderedSets()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var routine = await CreateRoutineAsync(database);
        var session = await database.WorkoutService.StartAsync(routine.Id);
        var exercise = session.Exercises.First();
        var firstSet = await database.WorkoutService.AddSetAsync(exercise.Id);
        await database.WorkoutService.UpdateSetAsync(firstSet.Id, new WorkoutSetInput(30m, 12, true));
        await database.WorkoutService.SetCompletedAsync(firstSet.Id, true);
        var secondSet = await database.WorkoutService.AddSetAsync(exercise.Id);
        await database.WorkoutService.UpdateSetAsync(secondSet.Id, new WorkoutSetInput(60m, 8, false));
        await database.WorkoutService.SetCompletedAsync(secondSet.Id, true);
        await database.WorkoutService.CompleteAsync(session.Id);

        var details = await database.HistoryService.GetByIdAsync(session.Id);

        Assert.NotNull(details);
        var loadedExercise = Assert.Single(details.Exercises);
        Assert.Equal([1, 2], loadedExercise.Sets.Select(item => item.SetNumber));
        Assert.Equal(exercise.ExerciseName, loadedExercise.ExerciseName);
    }

    [Fact]
    public async Task GetPreviousCompletedForRoutine_ReturnsOnlyTheLatestMatchingRoutine()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var firstRoutine = await CreateRoutineAsync(database);
        var exerciseId = firstRoutine.Exercises.Single().ExerciseId;
        var secondRoutine = await database.RoutineService.CreateAsync(
            new RoutineInput($"Other {Guid.NewGuid():N}", [exerciseId]));

        var firstMatching = await StartWorkoutWithCompletedSetAsync(database, firstRoutine.Id, 40m, 10);
        await database.WorkoutService.CompleteAsync(firstMatching.Id);
        var otherRoutine = await StartWorkoutWithCompletedSetAsync(database, secondRoutine.Id, 90m, 5);
        await database.WorkoutService.CompleteAsync(otherRoutine.Id);
        var latestMatching = await StartWorkoutWithCompletedSetAsync(database, firstRoutine.Id, 45m, 8);
        await database.WorkoutService.CompleteAsync(latestMatching.Id);
        var current = await StartWorkoutWithCompletedSetAsync(database, firstRoutine.Id, 50m, 6);
        await database.WorkoutService.CompleteAsync(current.Id);

        var previous = await database.HistoryService
            .GetPreviousCompletedForRoutineAsync(current.Id);

        Assert.NotNull(previous);
        Assert.Equal(latestMatching.Id, previous.Id);
        Assert.NotEqual(otherRoutine.Id, previous.Id);
    }

    [Fact]
    public void CalculateVolume_UsesOnlyCompletedNonWarmupSets()
    {
        var exercise = new WorkoutExercise
        {
            Sets =
            [
                new WorkoutSet { Weight = 100m, Repetitions = 5, IsCompleted = true },
                new WorkoutSet { Weight = 50m, Repetitions = 10, IsCompleted = true, IsWarmup = true },
                new WorkoutSet { Weight = 120m, Repetitions = 3, IsCompleted = false }
            ]
        };
        var session = new WorkoutSession { Exercises = [exercise] };
        var service = new HistoryService(null!, null!);

        Assert.Equal(500m, service.CalculateVolume(exercise));
        Assert.Equal(500m, service.CalculateVolume(session));
        Assert.Equal(2, service.CountCompletedSets(session));
    }

    [Fact]
    public void CalculateDuration_UsesStoredStartAndFinishTimes()
    {
        var startedAt = new DateTimeOffset(2026, 7, 13, 10, 0, 0, TimeSpan.Zero);
        var service = new HistoryService(null!, null!);

        Assert.Equal(
            TimeSpan.FromMinutes(75),
            service.CalculateDuration(new WorkoutSession
            {
                StartedAt = startedAt,
                FinishedAt = startedAt.AddMinutes(75)
            }));
        Assert.Equal(
            TimeSpan.Zero,
            service.CalculateDuration(new WorkoutSession { StartedAt = startedAt }));
    }

    [Fact]
    public async Task GetCompletedWorkouts_WithDateFilter_ReturnsOnlyMatchingSessions()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var routine = await CreateRoutineAsync(database);
        var session = await StartWorkoutWithCompletedSetAsync(database, routine.Id, 20m, 10);
        var completed = await database.WorkoutService.CompleteAsync(session.Id);

        var included = await database.HistoryService.GetCompletedAsync(
            completed.StartedAt.AddMinutes(-1),
            completed.StartedAt.AddMinutes(1));
        var excluded = await database.HistoryService.GetCompletedAsync(
            completed.StartedAt.AddMinutes(1),
            null);

        Assert.Single(included);
        Assert.Empty(excluded);
    }

    private static async Task<Routine> CreateRoutineAsync(SqliteTestDatabase database)
    {
        var exercise = (await database.ExerciseService.GetAllAsync()).First();
        return await database.RoutineService.CreateAsync(
            new RoutineInput($"History {Guid.NewGuid():N}", [exercise.Id]));
    }

    private static async Task<WorkoutSession> StartWorkoutWithCompletedSetAsync(
        SqliteTestDatabase database,
        int routineId,
        decimal weight,
        int repetitions)
    {
        var session = await database.WorkoutService.StartAsync(routineId);
        var workoutSet = await database.WorkoutService.AddSetAsync(session.Exercises.First().Id);
        await database.WorkoutService.UpdateSetAsync(
            workoutSet.Id,
            new WorkoutSetInput(weight, repetitions, false));
        await database.WorkoutService.SetCompletedAsync(workoutSet.Id, true);
        return session;
    }
}
