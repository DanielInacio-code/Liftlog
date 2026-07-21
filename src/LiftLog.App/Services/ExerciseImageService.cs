using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Platform;

namespace LiftLog.App.Services;

public sealed class ExerciseImageService : IExerciseImageService
{
    private const float DetailMaximumDimension = 1600;
    private const float ThumbnailDimension = 256;
    private const float DetailQuality = 0.88f;
    private const float ThumbnailQuality = 0.82f;

    private static string ImagesDirectory =>
        Path.Combine(FileSystem.AppDataDirectory, "exercise-images");

    public async Task<string?> PickAndSaveAsync(CancellationToken cancellationToken = default)
    {
        var results = await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions
        {
            Title = Resources.Strings.AppText.ChooseExercisePhoto,
            SelectionLimit = 1
        });
        var result = results.FirstOrDefault();

        if (result is null)
        {
            return null;
        }

        Directory.CreateDirectory(ImagesDirectory);

        var detailPath = Path.Combine(ImagesDirectory, $"{Guid.NewGuid():N}.jpg");
        var thumbnailPath = ExerciseImagePaths.GetThumbnailPath(detailPath);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var source = await result.OpenReadAsync();
            using var original = new PlatformImageLoadingService().FromStream(source);
            using var detail = original.Downsize(
                DetailMaximumDimension,
                DetailMaximumDimension,
                disposeOriginal: false);

            await using (var target = File.Create(detailPath))
            {
                await detail.SaveAsync(target, ImageFormat.Jpeg, DetailQuality);
            }

            cancellationToken.ThrowIfCancellationRequested();
            using var thumbnail = original.Resize(
                ThumbnailDimension,
                ThumbnailDimension,
                ResizeMode.Bleed,
                disposeOriginal: false);

            await using (var target = File.Create(thumbnailPath))
            {
                await thumbnail.SaveAsync(target, ImageFormat.Jpeg, ThumbnailQuality);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return detailPath;
        }
        catch
        {
            DeleteFileIfPresent(detailPath);
            DeleteFileIfPresent(thumbnailPath);
            throw;
        }
    }

    public void DeleteIfOwned(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            return;
        }

        var directory = Path.GetFullPath(ImagesDirectory)
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(imagePath);

        if (!candidate.StartsWith(directory, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var detailPath = ExerciseImagePaths.IsThumbnail(candidate)
            ? ExerciseImagePaths.GetDetailPathFromThumbnail(candidate)
            : candidate;
        var thumbnailPath = ExerciseImagePaths.GetThumbnailPath(detailPath);

        DeleteFileIfPresent(detailPath);
        DeleteFileIfPresent(thumbnailPath);
    }

    private static void DeleteFileIfPresent(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
