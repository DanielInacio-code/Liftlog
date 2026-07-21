using System.Globalization;
using LiftLog.Core.Models;

namespace LiftLog.App.ViewModels;

public sealed class BodyMeasurementListItem
{
    private static readonly CultureInfo DisplayCulture = CultureInfo.GetCultureInfo("en-GB");

    public BodyMeasurementListItem(BodyMeasurement measurement)
    {
        Id = measurement.Id;
        RecordedAt = measurement.RecordedAt;
        Date = measurement.RecordedAt.ToLocalTime().ToString("dd MMM yyyy", DisplayCulture);
        Notes = measurement.Notes;
        Details = string.Join("  ·  ", new[]
        {
            measurement.WeightKg is { } weight ? $"{weight.ToString("0.##", DisplayCulture)} kg" : null,
            measurement.BodyFatPercentage is { } bodyFat ? $"{bodyFat.ToString("0.##", DisplayCulture)}% fat" : null,
            measurement.WaistCm is { } waist ? $"{waist.ToString("0.##", DisplayCulture)} cm waist" : null
        }.Where(value => value is not null));
    }

    public int Id { get; }

    public DateTimeOffset RecordedAt { get; }

    public string Date { get; }

    public string Details { get; }

    public string? Notes { get; }

    public bool HasNotes => !string.IsNullOrWhiteSpace(Notes);
}
