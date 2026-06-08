using SpendingApi.Domain.Primitives;

namespace SpendingApi.Application.Abstractions;

public interface IReadOnlyRepository<T>
{
    Task<Result<T>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> FindAsync(ISpecification<T> specification, CancellationToken cancellationToken = default);
}
