using LiftLog.Core.Services;
using LiftLog.Tests.Support;

namespace LiftLog.Tests;

public class BodyMeasurementServiceTests
{
    [Fact]
    public async Task Measurements_CanBeSavedListedAndDeleted()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var earlier = new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);
        var later = earlier.AddDays(7);

        var first = await database.BodyMeasurementService.CreateAsync(
            new BodyMeasurementInput(earlier, 80.5m, 18.2m, 84m, "Morning"));
        var second = await database.BodyMeasurementService.CreateAsync(
            new BodyMeasurementInput(later, 79.8m, null, null));

        var saved = await database.BodyMeasurementService.GetAllAsync();

        Assert.Equal([second.Id, first.Id], saved.Select(item => item.Id));
        Assert.Equal(80.5m, saved[1].WeightKg);
        Assert.Equal(18.2m, saved[1].BodyFatPercentage);
        Assert.Equal(84m, saved[1].WaistCm);
        Assert.Equal("Morning", saved[1].Notes);

        await database.BodyMeasurementService.DeleteAsync(first.Id);

        var remaining = await database.BodyMeasurementService.GetAllAsync();
        Assert.Equal(second.Id, Assert.Single(remaining).Id);
    }

    [Fact]
    public async Task Measurement_RequiresAtLeastOneValue()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();

        await Assert.ThrowsAsync<BodyMeasurementValidationException>(() =>
            database.BodyMeasurementService.CreateAsync(
                new BodyMeasurementInput(DateTimeOffset.UtcNow, null, null, null)));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1001)]
    public async Task Measurement_RejectsInvalidBodyWeight(double value)
    {
        await using var database = await SqliteTestDatabase.CreateAsync();

        await Assert.ThrowsAsync<BodyMeasurementValidationException>(() =>
            database.BodyMeasurementService.CreateAsync(
                new BodyMeasurementInput(
                    DateTimeOffset.UtcNow,
                    Convert.ToDecimal(value),
                    null,
                    null)));
    }
}
