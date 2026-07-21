using LiftLog.Core.Models;
using LiftLog.Core.Services;
using LiftLog.Tests.Support;

namespace LiftLog.Tests;

public class WorkoutServiceTests
{
    [Fact]
    public async Task HasActiveWorkout_UsesTheSessionLifecycle()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var (routine, _) = await CreateRoutineAsync(database);

        Assert.False(await database.WorkoutService.HasActiveAsync());

        var session = await database.WorkoutService.StartAsync(routine.Id);

        Assert.True(await database.WorkoutService.HasActiveAsync());

        await database.WorkoutService.CancelAsync(session.Id);

        Assert.False(await database.WorkoutService.HasActiveAsync());
    }

    [Fact]
    public async Task StartWorkout_FromRoutine_CreatesOrderedSnapshotsAndCanBeRecovered()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var (routine, exerciseIds) = await CreateRoutineAsync(database, 3);

        var started = await database.WorkoutService.StartAsync(routine.Id);
        var recovered = await database.WorkoutService.GetActiveAsync();

        Assert.Equal(WorkoutStatus.InProgress, started.Status);
        Assert.Equal(routine.Name, started.RoutineName);
        Assert.NotNull(recovered);
        Assert.Equal(started.Id, recovered.Id);
        Assert.Equal(
            exerciseIds,
            recovered.Exercises.OrderBy(item => item.Position).Select(item => item.ExerciseId!.Value));
        Assert.All(recovered.Exercises, item => Assert.False(string.IsNullOrWhiteSpace(item.ExerciseName)));
    }

    [Fact]
    public async Task ActiveWorkoutOverview_ReturnsOnlyTheCurrentBannerData()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var (routine, _) = await CreateRoutineAsync(database, 2);
        var session = await database.WorkoutService.StartAsync(routine.Id);
        var orderedExercises = session.Exercises.OrderBy(item => item.Position).ToArray();
        var firstSet = await database.WorkoutService.AddSetAsync(orderedExercises[0].Id);
        await database.WorkoutService.UpdateSetAndCompletionAsync(
            firstSet.Id,
            new WorkoutSetInput(50m, 8, false),
            true);

        var overview = await database.WorkoutService.GetActiveOverviewAsync();

        Assert.NotNull(overview);
        Assert.Equal(
            session.StartedAt.ToUnixTimeMilliseconds(),
            overview.StartedAt.ToUnixTimeMilliseconds());
        Assert.Equal(session.RoutineName, overview.RoutineName);
        Assert.Equal(orderedExercises[1].ExerciseName, overview.CurrentExerciseName);

        await database.WorkoutService.CancelAsync(session.Id);
        Assert.Null(await database.WorkoutService.GetActiveOverviewAsync());
    }

    [Fact]
    public async Task StartWorkout_CopiesPlannedRoutineSetsAsIncompleteSets()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var exercise = (await database.ExerciseService.GetAllAsync()).First();
        var plan = new RoutineExerciseInput(
            exercise.Id,
            [
                new RoutineSetInput(20m, 12, true),
                new RoutineSetInput(80m, 5, false, 9.5, TrainingSetType.Failure)
            ]);
        var routine = await database.RoutineService.CreateAsync(
            new RoutineInput("Routine with sets", [exercise.Id], [plan]));

        var started = await database.WorkoutService.StartAsync(routine.Id);

        var sets = Assert.Single(started.Exercises).Sets.OrderBy(item => item.SetNumber).ToArray();
        Assert.Equal([1, 2], sets.Select(item => item.SetNumber));
        Assert.Equal(20m, sets[0].Weight);
        Assert.Equal(12, sets[0].Repetitions);
        Assert.True(sets[0].IsWarmup);
        Assert.Equal(80m, sets[1].Weight);
        Assert.Null(sets[1].Rpe);
        Assert.Equal(TrainingSetType.Failure, sets[1].SetType);
        Assert.False(sets[1].IsCompleted);
    }

    [Fact]
    public async Task StartWorkout_PrefillsWeightAndRepetitionsFromPreviousRoutineWorkout_WithoutRpe()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var exercise = (await database.ExerciseService.GetAllAsync()).First();
        var plan = new RoutineExerciseInput(
            exercise.Id,
            [new RoutineSetInput(40m, 12, false, 7)]);
        var routine = await database.RoutineService.CreateAsync(
            new RoutineInput("Progressive routine", [exercise.Id], [plan]));

        var previousSession = await database.WorkoutService.StartAsync(routine.Id);
        var previousSet = Assert.Single(Assert.Single(previousSession.Exercises).Sets);
        await database.WorkoutService.UpdateSetAndCompletionAsync(
            previousSet.Id,
            new WorkoutSetInput(60m, 8, false, 9),
            true);
        await database.WorkoutService.CompleteAsync(previousSession.Id);

        var currentSession = await database.WorkoutService.StartAsync(routine.Id);
        var currentSet = Assert.Single(Assert.Single(currentSession.Exercises).Sets);

        Assert.Equal(60m, currentSet.Weight);
        Assert.Equal(8, currentSet.Repetitions);
        Assert.Null(currentSet.Rpe);
        Assert.False(currentSet.IsCompleted);
    }

    [Fact]
    public async Task StartWorkout_WhenAnotherWorkoutIsActive_ThrowsValidationException()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var (activeRoutine, _) = await CreateRoutineAsync(database);
        var (otherRoutine, _) = await CreateRoutineAsync(database);
        var activeSession = await database.WorkoutService.StartAsync(activeRoutine.Id);

        var exception = await Assert.ThrowsAsync<WorkoutValidationException>(() =>
            database.WorkoutService.StartAsync(otherRoutine.Id));

        Assert.Contains("in progress", exception.Message);
        var recovered = await database.WorkoutService.GetActiveAsync();
        Assert.NotNull(recovered);
        Assert.Equal(activeSession.Id, recovered.Id);
        Assert.Equal(activeRoutine.Id, recovered.RoutineId);
    }

    [Fact]
    public async Task StartWorkout_FromEmptyRoutine_ThrowsValidationException()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var routine = await database.RoutineService.CreateAsync(new RoutineInput("Empty routine", []));

        var exception = await Assert.ThrowsAsync<WorkoutValidationException>(() =>
            database.WorkoutService.StartAsync(routine.Id));

        Assert.Contains("at least one exercise", exception.Message);
    }

    [Fact]
    public async Task SetChanges_ArePersistedImmediately()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var (routine, _) = await CreateRoutineAsync(database);
        var session = await database.WorkoutService.StartAsync(routine.Id);
        var workoutExercise = session.Exercises.First();

        var added = await database.WorkoutService.AddSetAsync(workoutExercise.Id);
        await database.WorkoutService.UpdateSetAsync(
            added.Id,
            new WorkoutSetInput(82.125m, 8, false));
        await database.WorkoutService.SetCompletedAsync(added.Id, true);

        var recovered = await database.WorkoutService.GetActiveAsync();
        var saved = Assert.Single(Assert.Single(recovered!.Exercises).Sets);
        Assert.Equal(82.125m, saved.Weight);
        Assert.Equal(8, saved.Repetitions);
        Assert.True(saved.IsCompleted);
        Assert.NotNull(saved.CompletedAt);
    }

    [Fact]
    public async Task UpdateSetAndCompletion_PersistsValuesAndCompletionAtomically()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var (routine, _) = await CreateRoutineAsync(database);
        var session = await database.WorkoutService.StartAsync(routine.Id);
        var set = await database.WorkoutService.AddSetAsync(session.Exercises.First().Id);

        var saved = await database.WorkoutService.UpdateSetAndCompletionAsync(
            set.Id,
            new WorkoutSetInput(91.25m, 6, false, 9, TrainingSetType.Failure),
            true);

        Assert.Equal(91.25m, saved.Weight);
        Assert.Equal(6, saved.Repetitions);
        Assert.Equal(9, saved.Rpe);
        Assert.Equal(TrainingSetType.Failure, saved.SetType);
        Assert.True(saved.IsCompleted);
        Assert.NotNull(saved.CompletedAt);

        var recovered = await database.WorkoutService.GetActiveAsync();
        var recoveredSet = Assert.Single(Assert.Single(recovered!.Exercises).Sets);
        Assert.Equal(saved.Weight, recoveredSet.Weight);
        Assert.True(recoveredSet.IsCompleted);
    }

    [Fact]
    public async Task ActiveWorkoutChanged_CarriesStartedSessionAndClearsAfterCancel()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var (routine, _) = await CreateRoutineAsync(database);
        var notifications = new List<WorkoutSession?>();
        database.WorkoutService.ActiveWorkoutChanged += (_, eventArgs) =>
            notifications.Add(eventArgs.Workout);

        var session = await database.WorkoutService.StartAsync(routine.Id);
        await database.WorkoutService.CancelAsync(session.Id);

        Assert.Equal(2, notifications.Count);
        Assert.Equal(session.Id, notifications[0]!.Id);
        Assert.Null(notifications[1]);
    }

    [Theory]
    [InlineData(-1, 8)]
    [InlineData(50, -1)]
    public async Task UpdateSet_WithNegativeValues_ThrowsValidationException(
        double weight,
        int repetitions)
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var (routine, _) = await CreateRoutineAsync(database);
        var session = await database.WorkoutService.StartAsync(routine.Id);
        var set = await database.WorkoutService.AddSetAsync(session.Exercises.First().Id);

        await Assert.ThrowsAsync<WorkoutValidationException>(() =>
            database.WorkoutService.UpdateSetAsync(
                set.Id,
                new WorkoutSetInput((decimal)weight, repetitions, false)));
    }

    [Fact]
    public async Task UpdateSet_WithRpeOutsideRange_ThrowsValidationException()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var (routine, _) = await CreateRoutineAsync(database);
        var session = await database.WorkoutService.StartAsync(routine.Id);
        var set = await database.WorkoutService.AddSetAsync(session.Exercises.First().Id);

        await Assert.ThrowsAsync<WorkoutValidationException>(() =>
            database.WorkoutService.UpdateSetAsync(
                set.Id,
                new WorkoutSetInput(50m, 8, false, 11)));
    }

    [Fact]
    public async Task DeleteSet_RenumbersRemainingSets()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var (routine, _) = await CreateRoutineAsync(database);
        var session = await database.WorkoutService.StartAsync(routine.Id);
        var exerciseId = session.Exercises.First().Id;
        var first = await database.WorkoutService.AddSetAsync(exerciseId);
        await database.WorkoutService.AddSetAsync(exerciseId);
        await database.WorkoutService.AddSetAsync(exerciseId);

        await database.WorkoutService.DeleteSetAsync(first.Id);

        var recovered = await database.WorkoutService.GetActiveAsync();
        Assert.Equal(
            [1, 2],
            recovered!.Exercises.First().Sets.OrderBy(item => item.SetNumber).Select(item => item.SetNumber));
    }

    [Fact]
    public async Task CompleteWorkout_WithoutCompletedSets_ThrowsValidationException()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var (routine, _) = await CreateRoutineAsync(database);
        var session = await database.WorkoutService.StartAsync(routine.Id);
        await database.WorkoutService.AddSetAsync(session.Exercises.First().Id);

        var exception = await Assert.ThrowsAsync<WorkoutValidationException>(() =>
            database.WorkoutService.CompleteAsync(session.Id));

        Assert.Contains("at least one set", exception.Message);
    }

    [Fact]
    public async Task CompleteWorkout_WithCompletedSet_ClearsActiveWorkoutAndStoresFinishTime()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var (routine, _) = await CreateRoutineAsync(database);
        var session = await database.WorkoutService.StartAsync(routine.Id);
        var set = await database.WorkoutService.AddSetAsync(session.Exercises.First().Id);
        await database.WorkoutService.SetCompletedAsync(set.Id, true);

        var completed = await database.WorkoutService.CompleteAsync(session.Id);

        Assert.Equal(WorkoutStatus.Completed, completed.Status);
        Assert.NotNull(completed.FinishedAt);
        Assert.Null(await database.WorkoutService.GetActiveAsync());
    }

    [Fact]
    public async Task CancelWorkout_SetsCancelledStatusAndClearsActiveWorkout()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var (routine, _) = await CreateRoutineAsync(database);
        var session = await database.WorkoutService.StartAsync(routine.Id);

        var cancelled = await database.WorkoutService.CancelAsync(session.Id);

        Assert.Equal(WorkoutStatus.Cancelled, cancelled.Status);
        Assert.NotNull(cancelled.FinishedAt);
        Assert.Null(await database.WorkoutService.GetActiveAsync());
    }

    [Fact]
    public async Task CompleteWorkout_SubscriberFailure_DoesNotReportCommittedWorkoutAsFailed()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var (routine, _) = await CreateRoutineAsync(database);
        var session = await database.WorkoutService.StartAsync(routine.Id);
        var set = await database.WorkoutService.AddSetAsync(session.Exercises.First().Id);
        await database.WorkoutService.SetCompletedAsync(set.Id, true);
        database.WorkoutService.ActiveWorkoutChanged += (_, _) =>
            throw new InvalidOperationException("Subscriber failed after the commit.");

        var completed = await database.WorkoutService.CompleteAsync(session.Id);

        Assert.Equal(WorkoutStatus.Completed, completed.Status);
        Assert.NotEmpty(completed.Exercises);
        Assert.Null(await database.WorkoutService.GetActiveAsync());
    }

    [Fact]
    public async Task CancelWorkout_SubscriberFailure_DoesNotReportCommittedWorkoutAsFailed()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var (routine, _) = await CreateRoutineAsync(database);
        var session = await database.WorkoutService.StartAsync(routine.Id);
        database.WorkoutService.ActiveWorkoutChanged += (_, _) =>
            throw new InvalidOperationException("Subscriber failed after the commit.");

        var cancelled = await database.WorkoutService.CancelAsync(session.Id);

        Assert.Equal(WorkoutStatus.Cancelled, cancelled.Status);
        Assert.NotEmpty(cancelled.Exercises);
        Assert.Null(await database.WorkoutService.GetActiveAsync());
    }

    [Fact]
    public async Task PreviousPerformance_ReturnsOnlyCompletedWorkingSets()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var (routine, exerciseIds) = await CreateRoutineAsync(database);
        var firstSession = await database.WorkoutService.StartAsync(routine.Id);
        var exercise = firstSession.Exercises.First();
        var warmup = await database.WorkoutService.AddSetAsync(exercise.Id);
        await database.WorkoutService.UpdateSetAsync(warmup.Id, new WorkoutSetInput(20m, 10, true));
        await database.WorkoutService.SetCompletedAsync(warmup.Id, true);
        var working = await database.WorkoutService.AddSetAsync(exercise.Id);
        await database.WorkoutService.UpdateSetAsync(
            working.Id,
            new WorkoutSetInput(60m, 8, false, 8.5));
        await database.WorkoutService.SetCompletedAsync(working.Id, true);
        await database.WorkoutService.CompleteAsync(firstSession.Id);

        var currentSession = await database.WorkoutService.StartAsync(routine.Id);
        var previous = await database.WorkoutService.GetPreviousPerformanceAsync(
            exerciseIds[0],
            currentSession.Id);

        Assert.NotNull(previous);
        var previousSet = Assert.Single(previous.Sets);
        Assert.Equal(60m, previousSet.Weight);
        Assert.Equal(8, previousSet.Repetitions);
        Assert.Equal(8.5, previousSet.Rpe);
        Assert.NotNull(previous.AllSets);
        Assert.Collection(
            previous.AllSets,
            item =>
            {
                Assert.Equal(20m, item.Weight);
                Assert.Equal(10, item.Repetitions);
                Assert.True(item.IsWarmup);
                Assert.Equal(1, item.SetNumber);
            },
            item =>
            {
                Assert.Equal(60m, item.Weight);
                Assert.Equal(8, item.Repetitions);
                Assert.False(item.IsWarmup);
                Assert.Equal(2, item.SetNumber);
            });
    }

    [Fact]
    public async Task PreviousPerformance_IsScopedToTheCurrentRoutine()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var exercise = (await database.ExerciseService.GetAllAsync()).First();
        var upperA = await database.RoutineService.CreateAsync(
            new RoutineInput("Upper A", [exercise.Id]));
        var upperB = await database.RoutineService.CreateAsync(
            new RoutineInput("Upper B", [exercise.Id]));

        var upperASession = await database.WorkoutService.StartAsync(upperA.Id);
        var upperASet = await database.WorkoutService.AddSetAsync(
            Assert.Single(upperASession.Exercises).Id);
        await database.WorkoutService.UpdateSetAndCompletionAsync(
            upperASet.Id,
            new WorkoutSetInput(40m, 12, false),
            true);
        await database.WorkoutService.CompleteAsync(upperASession.Id);

        var upperBSession = await database.WorkoutService.StartAsync(upperB.Id);
        var upperBSet = await database.WorkoutService.AddSetAsync(
            Assert.Single(upperBSession.Exercises).Id);
        await database.WorkoutService.UpdateSetAndCompletionAsync(
            upperBSet.Id,
            new WorkoutSetInput(80m, 6, false),
            true);
        await database.WorkoutService.CompleteAsync(upperBSession.Id);

        var currentUpperA = await database.WorkoutService.StartAsync(upperA.Id);
        var previous = await database.WorkoutService.GetPreviousPerformanceAsync(
            exercise.Id,
            currentUpperA.Id);

        Assert.NotNull(previous);
        var previousSet = Assert.Single(previous.Sets);
        Assert.Equal(40m, previousSet.Weight);
        Assert.Equal(12, previousSet.Repetitions);
    }

    [Fact]
    public async Task ActiveWorkout_ExercisesCanBeAddedMovedReplacedAndRemoved()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var exercises = (await database.ExerciseService.GetAllAsync()).Take(3).ToArray();
        var routine = await database.RoutineService.CreateAsync(
            new RoutineInput("Editable workout", [exercises[0].Id]));
        var session = await database.WorkoutService.StartAsync(routine.Id);

        var added = await database.WorkoutService.AddExerciseAsync(session.Id, exercises[1].Id);

        Assert.Equal(exercises[1].Id, added.ExerciseId);
        Assert.Equal(exercises[1].Name, added.ExerciseName);
        Assert.Single(added.Sets);

        await database.WorkoutService.MoveExerciseAsync(added.Id, 0);
        var moved = await database.WorkoutService.GetActiveAsync();
        Assert.Equal(
            [exercises[1].Id, exercises[0].Id],
            moved!.Exercises.OrderBy(item => item.Position).Select(item => item.ExerciseId!.Value));

        var replaced = await database.WorkoutService.ReplaceExerciseAsync(added.Id, exercises[2].Id);
        Assert.Equal(exercises[2].Id, replaced.ExerciseId);
        Assert.Equal(exercises[2].Name, replaced.ExerciseName);
        Assert.Single(replaced.Sets);

        var original = moved.Exercises.Single(item => item.ExerciseId == exercises[0].Id);
        await database.WorkoutService.RemoveExerciseAsync(original.Id);
        var remaining = await database.WorkoutService.GetActiveAsync();
        var onlyExercise = Assert.Single(remaining!.Exercises);
        Assert.Equal(exercises[2].Id, onlyExercise.ExerciseId);
        Assert.Equal(0, onlyExercise.Position);
    }

    [Fact]
    public async Task ActiveWorkout_CannotContainTheSameExerciseTwice()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var (routine, exerciseIds) = await CreateRoutineAsync(database);
        var session = await database.WorkoutService.StartAsync(routine.Id);

        var exception = await Assert.ThrowsAsync<WorkoutValidationException>(() =>
            database.WorkoutService.AddExerciseAsync(session.Id, exerciseIds[0]));

        Assert.Contains("already", exception.Message);
    }

    [Fact]
    public async Task ActiveWorkout_CannotReplaceAnExerciseWithOneAlreadyInTheWorkout()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var (routine, exerciseIds) = await CreateRoutineAsync(database, 2);
        var session = await database.WorkoutService.StartAsync(routine.Id);
        var firstExercise = session.Exercises.Single(item => item.ExerciseId == exerciseIds[0]);

        var exception = await Assert.ThrowsAsync<WorkoutValidationException>(() =>
            database.WorkoutService.ReplaceExerciseAsync(firstExercise.Id, exerciseIds[1]));

        Assert.Contains("already", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RestTimer_StartsOffAndCanBePersistedForTheActiveExercise()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var (routine, _) = await CreateRoutineAsync(database);
        var session = await database.WorkoutService.StartAsync(routine.Id);
        var exercise = Assert.Single(session.Exercises);

        Assert.Equal(0, exercise.RestTimerSeconds);

        await database.WorkoutService.SetRestTimerAsync(exercise.Id, 90);

        var recovered = await database.WorkoutService.GetActiveAsync();
        Assert.Equal(90, Assert.Single(recovered!.Exercises).RestTimerSeconds);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3601)]
    public async Task RestTimer_RejectsDurationsOutsideTheSupportedRange(int seconds)
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var (routine, _) = await CreateRoutineAsync(database);
        var session = await database.WorkoutService.StartAsync(routine.Id);
        var exercise = Assert.Single(session.Exercises);

        await Assert.ThrowsAsync<WorkoutValidationException>(() =>
            database.WorkoutService.SetRestTimerAsync(exercise.Id, seconds));
    }

    [Fact]
    public async Task ExerciseNotes_AreOptionalAndPersistForTheActiveWorkout()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var (routine, _) = await CreateRoutineAsync(database);
        var session = await database.WorkoutService.StartAsync(routine.Id);
        var exercise = Assert.Single(session.Exercises);

        var saved = await database.WorkoutService.UpdateExerciseNotesAsync(
            exercise.Id,
            "  Seat position 4  ");

        Assert.Equal("Seat position 4", saved.Notes);
        var recovered = await database.WorkoutService.GetActiveAsync();
        Assert.Equal("Seat position 4", Assert.Single(recovered!.Exercises).Notes);

        var cleared = await database.WorkoutService.UpdateExerciseNotesAsync(exercise.Id, "   ");
        Assert.Null(cleared.Notes);
    }

    [Fact]
    public async Task ExerciseNotes_RejectMoreThanTwoThousandCharacters()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var (routine, _) = await CreateRoutineAsync(database);
        var session = await database.WorkoutService.StartAsync(routine.Id);
        var exercise = Assert.Single(session.Exercises);

        await Assert.ThrowsAsync<WorkoutValidationException>(() =>
            database.WorkoutService.UpdateExerciseNotesAsync(exercise.Id, new string('a', 2001)));
    }

    [Fact]
    public void CalculateDuration_UsesFinishTimeOrProvidedCurrentTime()
    {
        var startedAt = new DateTimeOffset(2026, 7, 13, 10, 0, 0, TimeSpan.Zero);
        var service = new WorkoutService(null!, null!);
        var active = new WorkoutSession { StartedAt = startedAt };
        var completed = new WorkoutSession
        {
            StartedAt = startedAt,
            FinishedAt = startedAt.AddMinutes(75)
        };

        Assert.Equal(TimeSpan.FromMinutes(30), service.CalculateDuration(active, startedAt.AddMinutes(30)));
        Assert.Equal(TimeSpan.FromMinutes(75), service.CalculateDuration(completed, startedAt.AddHours(4)));
    }

    private static async Task<(Routine Routine, int[] ExerciseIds)> CreateRoutineAsync(
        SqliteTestDatabase database,
        int exerciseCount = 1)
    {
        var exerciseIds = (await database.ExerciseService.GetAllAsync())
            .Take(exerciseCount)
            .Select(item => item.Id)
            .ToArray();
        var routine = await database.RoutineService.CreateAsync(
            new RoutineInput($"Routine {Guid.NewGuid():N}", exerciseIds));

        return (routine, exerciseIds);
    }
}
