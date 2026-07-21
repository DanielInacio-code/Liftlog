using LiftLog.Core.Models;

namespace LiftLog.Core.Services;

public interface IRoutineService
{
    Task<IReadOnlyList<Routine>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Routine?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<Routine> CreateAsync(RoutineInput input, CancellationToken cancellationToken = default);

    Task<Routine> UpdateAsync(int id, RoutineInput input, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
