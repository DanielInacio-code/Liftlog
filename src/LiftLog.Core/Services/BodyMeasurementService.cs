using LiftLog.Core.Data;
using LiftLog.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace LiftLog.Core.Services;

public sealed class BodyMeasurementService(
    IDbContextFactory<LiftLogDbContext> contextFactory,
    IDatabaseInitializer databaseInitializer) : IBodyMeasurementService
{
    public async Task<IReadOnlyList<BodyMeasurement>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.BodyMeasurements
            .AsNoTracking()
            .OrderByDescending(measurement => measurement.RecordedAt)
            .ThenByDescending(measurement => measurement.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<BodyMeasurement> CreateAsync(
        BodyMeasurementInput input,
        CancellationToken cancellationToken = default)
    {
        Validate(input);
        await databaseInitializer.InitializeAsync(cancellationToken);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var measurement = new BodyMeasurement
        {
            RecordedAt = input.RecordedAt,
            WeightKg = input.WeightKg,
            BodyFatPercentage = input.BodyFatPercentage,
            WaistCm = input.WaistCm,
            Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim()
        };
        context.BodyMeasurements.Add(measurement);
        await context.SaveChangesAsync(cancellationToken);
        return measurement;
    }

    public async Task DeleteAsync(
        int measurementId,
        CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var measurement = await context.BodyMeasurements
            .SingleOrDefaultAsync(item => item.Id == measurementId, cancellationToken)
            ?? throw new BodyMeasurementValidationException("The measurement was not found.");
        context.BodyMeasurements.Remove(measurement);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static void Validate(BodyMeasurementInput input)
    {
        if (input.WeightKg is null && input.BodyFatPercentage is null && input.WaistCm is null)
        {
            throw new BodyMeasurementValidationException("Enter at least one measurement.");
        }

        if (input.WeightKg is <= 0 or > 1000)
        {
            throw new BodyMeasurementValidationException("Body weight must be between 0 and 1,000 kg.");
        }

        if (input.BodyFatPercentage is < 0 or > 100)
        {
            throw new BodyMeasurementValidationException("Body fat must be between 0 and 100%.");
        }

        if (input.WaistCm is <= 0 or > 500)
        {
            throw new BodyMeasurementValidationException("Waist measurement must be between 0 and 500 cm.");
        }

        if (input.Notes?.Trim().Length > 500)
        {
            throw new BodyMeasurementValidationException("Measurement notes cannot exceed 500 characters.");
        }
    }
}
