using Microsoft.Extensions.Logging;
using SpendingApi.Application.Abstractions;
using SpendingApi.Domain.Events;

namespace SpendingApi.Infrastructure.Messaging;

public sealed class EventDispatcher : IEventDispatcher
{
    private readonly ILogger<EventDispatcher> _logger;

    public EventDispatcher(ILogger<EventDispatcher> logger)
    {
        _logger = logger;
    }

    public async Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Dispatching domain event {EventType} with id {EventId}",
            domainEvent.GetType().Name,
            domainEvent.EventId);

        // In production: publish to AWS SNS/SQS here
        await Task.CompletedTask;
    }
}
