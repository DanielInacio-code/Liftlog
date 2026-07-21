using LiftLog.Core.Models;
using LiftLog.Core.Services;
using LiftLog.Tests.Support;

namespace LiftLog.Tests;

public class ExerciseServiceTests
{
    [Theory]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("   ")]
    [InlineData(" A ")]
    public async Task CreateExercise_WithInvalidName_ThrowsValidationException(string name)
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var input = new ExerciseInput(name, MuscleGroup.Back, Equipment.Dumbbell, null);

        var exception = await Assert.ThrowsAsync<ExerciseValidationException>(() =>
            database.ExerciseService.CreateCustomAsync(input));

        Assert.Contains("between 2 and 100", exception.Message);
    }

    [Fact]
    public async Task CreateExercise_WithValidInput_TrimsAndPersistsCustomExercise()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var input = new ExerciseInput(
            "  Single-arm row  ",
            MuscleGroup.Back,
            Equipment.Dumbbell,
            "  Keep your back stable.  ");

        var created = await database.ExerciseService.CreateCustomAsync(input);
        var loaded = await database.ExerciseService.GetByIdAsync(created.Id);

        Assert.NotNull(loaded);
        Assert.True(loaded.IsCustom);
        Assert.Equal("Single-arm row", loaded.Name);
        Assert.Equal("Keep your back stable.", loaded.Instructions);
    }

    [Fact]
    public async Task CreateAndUpdateExercise_WithImagePath_PersistsImagePath()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var created = await database.ExerciseService.CreateCustomAsync(new ExerciseInput(
            "Ring row",
            MuscleGroup.Back,
            Equipment.Bodyweight,
            null,
            "/local/exercise-images/first.jpg"));

        Assert.Equal("/local/exercise-images/first.jpg", created.ImagePath);

        await database.ExerciseService.UpdateCustomAsync(created.Id, new ExerciseInput(
            created.Name,
            created.MuscleGroup,
            created.Equipment,
            created.Instructions,
            "/local/exercise-images/second.png"));

        var updated = await database.ExerciseService.GetByIdAsync(created.Id);
        Assert.NotNull(updated);
        Assert.Equal("/local/exercise-images/second.png", updated.ImagePath);
    }

    [Fact]
    public async Task CreateExercise_WithDuplicateNameIgnoringCase_ThrowsValidationException()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await database.ExerciseService.CreateCustomAsync(new ExerciseInput(
            "Y raise",
            MuscleGroup.Shoulders,
            Equipment.Dumbbell,
            null));

        var exception = await Assert.ThrowsAsync<ExerciseValidationException>(() =>
            database.ExerciseService.CreateCustomAsync(new ExerciseInput(
                "y raise",
                MuscleGroup.Shoulders,
                Equipment.Cable,
                null)));

        Assert.Contains("already exists", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateExercise_WithPredefinedExerciseName_ThrowsValidationException()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();

        var exception = await Assert.ThrowsAsync<ExerciseValidationException>(() =>
            database.ExerciseService.CreateCustomAsync(new ExerciseInput(
                "  BARBELL BENCH PRESS  ",
                MuscleGroup.Chest,
                Equipment.Barbell,
                null)));

        Assert.Contains("already exists", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteExercise_WhenExerciseIsPredefined_ThrowsValidationException()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var predefined = (await database.ExerciseService.GetAllAsync()).First();

        var exception = await Assert.ThrowsAsync<ExerciseValidationException>(() =>
            database.ExerciseService.DeleteCustomAsync(predefined.Id));

        Assert.Contains("built-in", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteExercise_WhenExerciseIsCustom_RemovesExercise()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var created = await database.ExerciseService.CreateCustomAsync(new ExerciseInput(
            "T-bar row",
            MuscleGroup.Back,
            Equipment.Barbell,
            null));

        await database.ExerciseService.DeleteCustomAsync(created.Id);

        Assert.Null(await database.ExerciseService.GetByIdAsync(created.Id));
    }

    [Fact]
    public async Task UpdateExercise_WhenExerciseIsPredefined_ThrowsValidationException()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var predefined = (await database.ExerciseService.GetAllAsync()).First();

        var exception = await Assert.ThrowsAsync<ExerciseValidationException>(() =>
            database.ExerciseService.UpdateCustomAsync(predefined.Id, new ExerciseInput(
                predefined.Name,
                predefined.MuscleGroup,
                predefined.Equipment,
                predefined.Instructions)));

        Assert.Contains("built-in", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateExercise_WhenExerciseIsCustom_PersistsChanges()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var created = await database.ExerciseService.CreateCustomAsync(new ExerciseInput(
            "Pullover",
            MuscleGroup.Back,
            Equipment.Dumbbell,
            null));

        await database.ExerciseService.UpdateCustomAsync(created.Id, new ExerciseInput(
            "Cable pullover",
            MuscleGroup.Back,
            Equipment.Cable,
            "Control the movement."));

        var updated = await database.ExerciseService.GetByIdAsync(created.Id);
        Assert.NotNull(updated);
        Assert.Equal("Cable pullover", updated.Name);
        Assert.Equal(Equipment.Cable, updated.Equipment);
    }

    [Fact]
    public async Task UpdateExercise_ToAnExistingNameIgnoringCaseAndSpaces_ThrowsValidationException()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await database.ExerciseService.CreateCustomAsync(new ExerciseInput(
            "Cable Y raise",
            MuscleGroup.Shoulders,
            Equipment.Cable,
            null));
        var exerciseToRename = await database.ExerciseService.CreateCustomAsync(new ExerciseInput(
            "Dumbbell Y raise",
            MuscleGroup.Shoulders,
            Equipment.Dumbbell,
            null));

        var exception = await Assert.ThrowsAsync<ExerciseValidationException>(() =>
            database.ExerciseService.UpdateCustomAsync(
                exerciseToRename.Id,
                new ExerciseInput(
                    "  CABLE Y RAISE  ",
                    MuscleGroup.Shoulders,
                    Equipment.Dumbbell,
                    null)));

        Assert.Contains("already exists", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetExercises_WithSearchAndMuscleFilter_ReturnsMatchingExercises()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();

        var exercises = await database.ExerciseService.GetAllAsync("bench press", MuscleGroup.Chest);

        Assert.Equal(
            [
                "Barbell bench press",
                "Decline Bench Press (Machine)",
                "Dumbbell bench press",
                "Incline Bench Press (Dumbbell)"
            ],
            exercises.Select(exercise => exercise.Name).ToArray());
        Assert.All(exercises, exercise => Assert.Equal(MuscleGroup.Chest, exercise.MuscleGroup));
        Assert.All(exercises, exercise =>
            Assert.Contains("bench press", exercise.Name, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("chest supported row")]
    [InlineData("chest support row")]
    [InlineData("chest-supported row")]
    [InlineData("t bar row")]
    [InlineData("t-bar row")]
    public async Task GetExercises_WithChestSupportedRowSearchVariations_FindsSameExercise(
        string searchText)
    {
        await using var database = await SqliteTestDatabase.CreateAsync();

        var exercises = await database.ExerciseService.GetAllAsync(searchText);

        var exercise = Assert.Single(exercises);
        Assert.Equal("Chest Supported Row / T-Bar Row", exercise.Name);
        Assert.Equal(MuscleGroup.Back, exercise.MuscleGroup);
        Assert.Equal(Equipment.Machine, exercise.Equipment);
    }

    [Theory]
    [InlineData("back", MuscleGroup.Back)]
    [InlineData("shoulder", MuscleGroup.Shoulders)]
    [InlineData("tricep", MuscleGroup.Triceps)]
    [InlineData("full body", MuscleGroup.FullBody)]
    public async Task GetExercises_WithMuscleGroupSearch_ReturnsThatMuscleGroup(
        string searchText,
        MuscleGroup expectedMuscleGroup)
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var marker = await database.ExerciseService.CreateCustomAsync(new ExerciseInput(
            $"Zeta movement {(int)expectedMuscleGroup}",
            expectedMuscleGroup,
            Equipment.None,
            null));

        var exercises = await database.ExerciseService.GetAllAsync(searchText);

        Assert.NotEmpty(exercises);
        Assert.Contains(exercises, exercise => exercise.Id == marker.Id);
        Assert.All(exercises, exercise => Assert.Equal(expectedMuscleGroup, exercise.MuscleGroup));
    }
}
