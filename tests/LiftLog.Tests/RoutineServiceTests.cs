using LiftLog.Core.Models;
using LiftLog.Core.Services;
using LiftLog.Tests.Support;

namespace LiftLog.Tests;

public class RoutineServiceTests
{
    [Theory]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("   ")]
    [InlineData(" A ")]
    public async Task CreateRoutine_WithInvalidName_ThrowsValidationException(string name)
    {
        await using var database = await SqliteTestDatabase.CreateAsync();

        var exception = await Assert.ThrowsAsync<RoutineValidationException>(() =>
            database.RoutineService.CreateAsync(new RoutineInput(name, [])));

        Assert.Contains("between 2 and 100", exception.Message);
    }

    [Fact]
    public async Task CreateRoutine_WithExercises_PersistsTheirOrder()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var exercises = await database.ExerciseService.GetAllAsync();
        var selected = exercises.Take(3).Select(exercise => exercise.Id).Reverse().ToArray();

        var created = await database.RoutineService.CreateAsync(
            new RoutineInput("  Upper body  ", selected));

        Assert.Equal("Upper body", created.Name);
            Assert.Equal(selected, created.Exercises.OrderBy(item => item.Position).Select(item => item.ExerciseId));
    }

    [Fact]
    public async Task CreateAndUpdateRoutine_PersistsPlannedSets()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var exercise = (await database.ExerciseService.GetAllAsync()).First();
        var initialPlan = new RoutineExerciseInput(
            exercise.Id,
            [
                new RoutineSetInput(20m, 12, true),
                new RoutineSetInput(60m, 8, false, 9.5, TrainingSetType.Failure)
            ]);

        var created = await database.RoutineService.CreateAsync(
            new RoutineInput("Planned routine", [exercise.Id], [initialPlan]));

        var createdSets = Assert.Single(created.Exercises).Sets.OrderBy(item => item.SetNumber).ToArray();
        Assert.Equal([1, 2], createdSets.Select(item => item.SetNumber));
        Assert.Equal(20m, createdSets[0].Weight);
        Assert.True(createdSets[0].IsWarmup);
        Assert.Equal(8, createdSets[1].Repetitions);
        Assert.Equal(9.5, createdSets[1].Rpe);
        Assert.Equal(TrainingSetType.Failure, createdSets[1].SetType);

        var updatedPlan = new RoutineExerciseInput(
            exercise.Id,
            [new RoutineSetInput(70.5m, 6, false, 8.5, TrainingSetType.Drop)]);
        var updated = await database.RoutineService.UpdateAsync(
            created.Id,
            new RoutineInput("Planned routine", [exercise.Id], [updatedPlan]));

        var updatedSet = Assert.Single(Assert.Single(updated.Exercises).Sets);
        Assert.Equal(1, updatedSet.SetNumber);
        Assert.Equal(70.5m, updatedSet.Weight);
        Assert.Equal(6, updatedSet.Repetitions);
        Assert.False(updatedSet.IsWarmup);
        Assert.Equal(8.5, updatedSet.Rpe);
        Assert.Equal(TrainingSetType.Drop, updatedSet.SetType);
    }

    [Fact]
    public async Task GetAllRoutines_LoadsExercisePreviewWithoutPlannedSets()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var exercise = (await database.ExerciseService.GetAllAsync()).First();
        var created = await database.RoutineService.CreateAsync(
            new RoutineInput(
                "Routine list projection",
                [exercise.Id],
                [new RoutineExerciseInput(
                    exercise.Id,
                    [new RoutineSetInput(75m, 6, false)])]));

        var listed = Assert.Single(
            await database.RoutineService.GetAllAsync(),
            item => item.Id == created.Id);
        var listedExercise = Assert.Single(listed.Exercises);

        Assert.Equal(exercise.Name, listedExercise.Exercise.Name);
        Assert.Empty(listedExercise.Sets);

        var detailed = await database.RoutineService.GetByIdAsync(created.Id);
        Assert.Single(Assert.Single(detailed!.Exercises).Sets);
    }

    [Fact]
    public async Task CreateRoutine_WithDuplicateNameIgnoringCase_ThrowsValidationException()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await database.RoutineService.CreateAsync(new RoutineInput("Strength A", []));

        var exception = await Assert.ThrowsAsync<RoutineValidationException>(() =>
            database.RoutineService.CreateAsync(new RoutineInput("  strength a  ", [])));

        Assert.Contains("already exists", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateRoutine_ToAnExistingNameIgnoringCaseAndSpaces_ThrowsValidationException()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await database.RoutineService.CreateAsync(new RoutineInput("Upper A", []));
        var routineToRename = await database.RoutineService.CreateAsync(
            new RoutineInput("Upper B", []));

        var exception = await Assert.ThrowsAsync<RoutineValidationException>(() =>
            database.RoutineService.UpdateAsync(
                routineToRename.Id,
                new RoutineInput("  UPPER A  ", [])));

        Assert.Contains("already exists", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateRoutine_WithDuplicateExercise_ThrowsValidationException()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var exercise = (await database.ExerciseService.GetAllAsync()).First();

        var exception = await Assert.ThrowsAsync<RoutineValidationException>(() =>
            database.RoutineService.CreateAsync(
                new RoutineInput("Repeated routine", [exercise.Id, exercise.Id])));

        Assert.Contains("more than once", exception.Message);
    }

    [Fact]
    public async Task CreateRoutine_WithMissingExercise_ThrowsValidationException()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();

        var exception = await Assert.ThrowsAsync<RoutineValidationException>(() =>
            database.RoutineService.CreateAsync(new RoutineInput("Invalid routine", [int.MaxValue])));

        Assert.Contains("no longer exist", exception.Message);
    }

    [Fact]
    public async Task UpdateRoutine_CanAddRemoveAndReorderExercises()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var exercises = (await database.ExerciseService.GetAllAsync()).Take(4).ToArray();
        var created = await database.RoutineService.CreateAsync(
            new RoutineInput("Initial workout", [exercises[0].Id, exercises[1].Id, exercises[2].Id]));

        var updatedOrder = new[] { exercises[2].Id, exercises[3].Id, exercises[0].Id };
        var updated = await database.RoutineService.UpdateAsync(
            created.Id,
            new RoutineInput("Updated workout", updatedOrder));

        Assert.Equal("Updated workout", updated.Name);
        Assert.Equal(
            updatedOrder,
            updated.Exercises.OrderBy(item => item.Position).Select(item => item.ExerciseId));
        Assert.DoesNotContain(updated.Exercises, item => item.ExerciseId == exercises[1].Id);
    }

    [Fact]
    public async Task DeleteRoutine_RemovesIt()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var created = await database.RoutineService.CreateAsync(new RoutineInput("Temporary routine", []));

        await database.RoutineService.DeleteAsync(created.Id);

        Assert.Null(await database.RoutineService.GetByIdAsync(created.Id));
    }

    [Fact]
    public async Task DeletingCustomExercise_RemovesItFromRoutine()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var customExercise = await database.ExerciseService.CreateCustomAsync(new ExerciseInput(
            "Single-arm press",
            LiftLog.Core.Models.MuscleGroup.Shoulders,
            LiftLog.Core.Models.Equipment.Dumbbell,
            null));
        var routine = await database.RoutineService.CreateAsync(
            new RoutineInput("Routine with custom exercise", [customExercise.Id]));

        await database.ExerciseService.DeleteCustomAsync(customExercise.Id);

        var updated = await database.RoutineService.GetByIdAsync(routine.Id);
        Assert.NotNull(updated);
        Assert.Empty(updated.Exercises);
    }
}
