namespace LiftLog.Core.Models;

public sealed class BodyMeasurement
{
    public int Id { get; set; }

    public DateTimeOffset RecordedAt { get; set; }

    public decimal? WeightKg { get; set; }

    public decimal? BodyFatPercentage { get; set; }

    public decimal? WaistCm { get; set; }

    public string? Notes { get; set; }
}
