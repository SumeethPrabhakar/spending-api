using SpendingApi.Domain.Events;

namespace SpendingApi.Application.Abstractions;

public interface IEventDispatcher
{
    Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default);
}
