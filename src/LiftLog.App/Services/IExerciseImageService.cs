namespace LiftLog.App.Services;

public interface IExerciseImageService
{
    Task<string?> PickAndSaveAsync(CancellationToken cancellationToken = default);

    void DeleteIfOwned(string? imagePath);
}
