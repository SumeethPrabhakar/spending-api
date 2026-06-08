using System.Collections.Concurrent;
using SpendingApi.Application.Abstractions;

namespace SpendingApi.Infrastructure.Idempotency;

// In production: replace with Redis or PostgreSQL-backed store
// In-memory store is NOT safe across multiple instances / restarts
public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private static readonly ConcurrentDictionary<string, IdempotencyResult> _store = new();
    private static readonly TimeSpan _expiry = TimeSpan.FromHours(24);

    public Task<IdempotencyResult?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        if (_store.TryGetValue(key, out var result))
        {
            // Expire old entries
            if (DateTimeOffset.UtcNow - result.CreatedAt < _expiry)
                return Task.FromResult<IdempotencyResult?>(result);

            _store.TryRemove(key, out _);
        }

        return Task.FromResult<IdempotencyResult?>(null);
    }

    public Task StoreAsync(string key, IdempotencyResult result, CancellationToken cancellationToken = default)
    {
        _store[key] = result;
        return Task.CompletedTask;
    }
}
