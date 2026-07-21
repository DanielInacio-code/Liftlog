namespace LiftLog.App.Services;

internal static class ExerciseImagePaths
{
    private const string ThumbnailSuffix = ".thumb.jpg";

    public static string GetThumbnailPath(string detailPath)
    {
        var directory = Path.GetDirectoryName(detailPath) ?? string.Empty;
        var fileName = Path.GetFileNameWithoutExtension(detailPath);
        return Path.Combine(directory, $"{fileName}{ThumbnailSuffix}");
    }

    public static bool IsThumbnail(string path) =>
        Path.GetFileName(path).EndsWith(ThumbnailSuffix, StringComparison.OrdinalIgnoreCase);

    public static string GetDetailPathFromThumbnail(string thumbnailPath)
    {
        var directory = Path.GetDirectoryName(thumbnailPath) ?? string.Empty;
        var fileName = Path.GetFileName(thumbnailPath);
        var detailName = fileName[..^ThumbnailSuffix.Length];
        return Path.Combine(directory, $"{detailName}.jpg");
    }
}
