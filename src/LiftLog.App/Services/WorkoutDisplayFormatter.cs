using System.Globalization;

namespace LiftLog.App.Services;

public static class WorkoutDisplayFormatter
{
    private static readonly CultureInfo DisplayCulture = CultureInfo.GetCultureInfo("en-GB");

    public static string FormatDate(DateTimeOffset value) =>
        value.ToLocalTime().ToString("dd MMM yyyy · HH:mm", DisplayCulture);

    public static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalMinutes < 1)
        {
            return "< 1 min";
        }

        return duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours} h {duration.Minutes} min"
            : $"{duration.Minutes} min";
    }

    public static string FormatVolume(decimal volume) =>
        $"{volume.ToString("0.##", DisplayCulture)} kg";

    public static string FormatCompactVolume(decimal volume)
    {
        if (volume >= 1_000_000)
        {
            return $"{(volume / 1_000_000).ToString("0.#", DisplayCulture)}m kg";
        }

        return volume >= 1_000
            ? $"{(volume / 1_000).ToString("0.#", DisplayCulture)}k kg"
            : FormatVolume(volume);
    }

    public static string FormatWeight(decimal weight) =>
        $"{weight.ToString("0.###", DisplayCulture)} kg";

    public static string FormatRpe(double rpe) =>
        rpe.ToString("0.#", DisplayCulture);
}
