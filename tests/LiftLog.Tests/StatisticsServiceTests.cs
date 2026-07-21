using LiftLog.Core.Models;
using LiftLog.Core.Services;
using LiftLog.Tests.Support;

namespace LiftLog.Tests;

public class StatisticsServiceTests
{
    [Fact]
    public void StatisticsCalculations_ExcludeWarmupAndIncompleteSets()
    {
        var sets = new[]
        {
            new WorkoutSet { Weight = 80m, Repetitions = 8, IsCompleted = true },
            new WorkoutSet { Weight = 100m, Repetitions = 3, IsCompleted = false },
            new WorkoutSet { Weight = 90m, Repetitions = 12, IsCompleted = true, IsWarmup = true },
            new WorkoutSet { Weight = 85m, Repetitions = 6, IsCompleted = true }
        };
        var service = new StatisticsService(null!, null!);

        Assert.Equal(1150m, service.CalculateVolume(sets));
        Assert.Equal(85m, service.CalculateMaximumWeight(sets));
        Assert.Equal(8, service.CalculateBestRepetitions(sets));
    }

    [Fact]
    public async Task GetExerciseProgress_UsesOnlyCompletedWorkingSetsAndSessions()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var (routine, exerciseId) = await CreateRoutineAsync(database);

        var first = await database.WorkoutService.StartAsync(routine.Id);
        await AddCompletedSetAsync(database, first, 50m, 10, false);
        await AddCompletedSetAsync(database, first, 60m, 5, true);
        await database.WorkoutService.CompleteAsync(first.Id);

        var second = await database.WorkoutService.StartAsync(routine.Id);
        await AddCompletedSetAsync(database, second, 55m, 8, false);
        await database.WorkoutService.CompleteAsync(second.Id);

        var cancelled = await database.WorkoutService.StartAsync(routine.Id);
        await AddCompletedSetAsync(database, cancelled, 100m, 1, false);
        await database.WorkoutService.CancelAsync(cancelled.Id);

        var progress = await database.StatisticsService.GetExerciseProgressAsync(exerciseId);

        Assert.Equal(55m, progress.BestWeight);
        Assert.Equal(10, progress.BestRepetitions);
        Assert.Equal(940m, progress.TotalVolume);
        Assert.Equal(2, progress.WorkoutCount);
        Assert.Equal([50m, 55m], progress.Points.Select(point => point.MaximumWeight));
    }

    [Fact]
    public async Task WeightPersonalRecord_RequiresHigherCompletedWorkingWeight()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var (routine, _) = await CreateRoutineAsync(database);
        var session = await database.WorkoutService.StartAsync(routine.Id);

        var first = await AddCompletedSetAsync(database, session, 50m, 8, false);
        var lower = await AddCompletedSetAsync(database, session, 45m, 10, false);
        var higher = await AddCompletedSetAsync(database, session, 55m, 6, false);
        var warmup = await AddCompletedSetAsync(database, session, 80m, 3, true);

        Assert.True(await database.StatisticsService.IsWeightPersonalRecordAsync(first.Id));
        Assert.False(await database.StatisticsService.IsWeightPersonalRecordAsync(lower.Id));
        Assert.True(await database.StatisticsService.IsWeightPersonalRecordAsync(higher.Id));
        Assert.False(await database.StatisticsService.IsWeightPersonalRecordAsync(warmup.Id));
    }

    [Fact]
    public async Task GetPersonalRecords_ForCompletedWorkout_ReturnsOnlyNewHigherWeights()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var (routine, _) = await CreateRoutineAsync(database);
        var firstSession = await database.WorkoutService.StartAsync(routine.Id);
        await AddCompletedSetAsync(database, firstSession, 50m, 8, false);
        await AddCompletedSetAsync(database, firstSession, 55m, 6, false);
        await database.WorkoutService.CompleteAsync(firstSession.Id);

        var secondSession = await database.WorkoutService.StartAsync(routine.Id);
        var equal = await AddCompletedSetAsync(database, secondSession, 55m, 8, false);
        var higher = await AddCompletedSetAsync(database, secondSession, 60m, 5, false);
        await database.WorkoutService.CompleteAsync(secondSession.Id);

        var records = await database.StatisticsService
            .GetWeightPersonalRecordSetIdsAsync(secondSession.Id);

        Assert.DoesNotContain(equal.Id, records);
        Assert.Contains(higher.Id, records);
        Assert.Single(records);
    }

    [Fact]
    public async Task WeightPersonalRecords_AreScopedToTheSameRoutine()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var (firstRoutine, exerciseId) = await CreateRoutineAsync(database);
        var secondRoutine = await database.RoutineService.CreateAsync(
            new RoutineInput($"Other {Guid.NewGuid():N}", [exerciseId]));

        var firstSession = await database.WorkoutService.StartAsync(firstRoutine.Id);
        await AddCompletedSetAsync(database, firstSession, 100m, 5, false);
        await database.WorkoutService.CompleteAsync(firstSession.Id);

        var secondSession = await database.WorkoutService.StartAsync(secondRoutine.Id);
        var routineRecord = await AddCompletedSetAsync(database, secondSession, 60m, 8, false);

        Assert.True(await database.StatisticsService.IsWeightPersonalRecordAsync(routineRecord.Id));

        var lowerInSameRoutine = await AddCompletedSetAsync(database, secondSession, 55m, 10, false);
        Assert.False(await database.StatisticsService.IsWeightPersonalRecordAsync(lowerInSameRoutine.Id));
    }

    [Fact]
    public async Task CancelledWorkout_DoesNotPreventFuturePersonalRecord()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var (routine, _) = await CreateRoutineAsync(database);

        var cancelled = await database.WorkoutService.StartAsync(routine.Id);
        await AddCompletedSetAsync(database, cancelled, 100m, 5, false);
        await database.WorkoutService.CancelAsync(cancelled.Id);

        var current = await database.WorkoutService.StartAsync(routine.Id);
        var set = await AddCompletedSetAsync(database, current, 60m, 5, false);

        Assert.True(await database.StatisticsService.IsWeightPersonalRecordAsync(set.Id));
    }

    [Fact]
    public async Task ExercisePersonalRecords_RecalculateLaterSetsAfterEarlierSetChanges()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var (routine, exerciseId) = await CreateRoutineAsync(database);
        var session = await database.WorkoutService.StartAsync(routine.Id);

        var first = await AddCompletedSetAsync(database, session, 50m, 8, false);
        var second = await AddCompletedSetAsync(database, session, 55m, 6, false);

        await database.WorkoutService.UpdateSetAsync(
            first.Id,
            new WorkoutSetInput(60m, 8, false));

        var afterEdit = await database.StatisticsService
            .GetWeightPersonalRecordSetIdsAsync(session.Id, exerciseId);

        Assert.Contains(first.Id, afterEdit);
        Assert.DoesNotContain(second.Id, afterEdit);

        await database.WorkoutService.SetCompletedAsync(first.Id, false);

        var afterReopen = await database.StatisticsService
            .GetWeightPersonalRecordSetIdsAsync(session.Id, exerciseId);

        Assert.DoesNotContain(first.Id, afterReopen);
        Assert.Contains(second.Id, afterReopen);
    }

    private static async Task<(Routine Routine, int ExerciseId)> CreateRoutineAsync(
        SqliteTestDatabase database)
    {
        var exercise = (await database.ExerciseService.GetAllAsync()).First();
        var routine = await database.RoutineService.CreateAsync(
            new RoutineInput($"Progress {Guid.NewGuid():N}", [exercise.Id]));
        return (routine, exercise.Id);
    }

    private static async Task<WorkoutSet> AddCompletedSetAsync(
        SqliteTestDatabase database,
        WorkoutSession session,
        decimal weight,
        int repetitions,
        bool isWarmup)
    {
        var workoutSet = await database.WorkoutService.AddSetAsync(session.Exercises.First().Id);
        await database.WorkoutService.UpdateSetAsync(
            workoutSet.Id,
            new WorkoutSetInput(weight, repetitions, isWarmup));
        return await database.WorkoutService.SetCompletedAsync(workoutSet.Id, true);
    }
}
