namespace LiftLog.Core.Services;

public sealed record BodyMeasurementInput(
    DateTimeOffset RecordedAt,
    decimal? WeightKg,
    decimal? BodyFatPercentage,
    decimal? WaistCm,
    string? Notes = null);
