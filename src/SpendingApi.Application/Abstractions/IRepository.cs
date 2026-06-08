namespace SpendingApi.Application.Abstractions;

public interface IRepository<T> : IReadOnlyRepository<T>
{
    Task SaveAsync(T entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(T entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
