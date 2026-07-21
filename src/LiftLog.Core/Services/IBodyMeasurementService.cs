using LiftLog.Core.Models;

namespace LiftLog.Core.Services;

public interface IBodyMeasurementService
{
    Task<IReadOnlyList<BodyMeasurement>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<BodyMeasurement> CreateAsync(
        BodyMeasurementInput input,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(int measurementId, CancellationToken cancellationToken = default);
}
