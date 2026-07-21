using LiftLog.Core.Models;

namespace LiftLog.Core.Services;

public interface IExerciseService
{
    Task<IReadOnlyList<Exercise>> GetAllAsync(
        string? searchText = null,
        MuscleGroup? muscleGroup = null,
        CancellationToken cancellationToken = default);

    Task<Exercise?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<Exercise> CreateCustomAsync(
        ExerciseInput input,
        CancellationToken cancellationToken = default);

    Task<Exercise> UpdateCustomAsync(
        int id,
        ExerciseInput input,
        CancellationToken cancellationToken = default);

    Task DeleteCustomAsync(int id, CancellationToken cancellationToken = default);
}
