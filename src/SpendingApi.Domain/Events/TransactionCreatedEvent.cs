namespace SpendingApi.Domain.Events;

public sealed record TransactionCreatedEvent(
    Guid TransactionId,
    Guid CustomerId) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
