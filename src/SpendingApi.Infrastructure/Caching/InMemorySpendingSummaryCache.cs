using Microsoft.Extensions.Caching.Memory;
using SpendingApi.Application.Abstractions;
using SpendingApi.Application.SpendingSummary;

namespace SpendingApi.Infrastructure.Caching;

// In production: replace with Redis-backed implementation
// Monthly summaries are a perfect cache candidate — expensive query, read-heavy, acceptable 5-min lag
public sealed class InMemorySpendingSummaryCache : ISpendingSummaryCache
{
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    public InMemorySpendingSummaryCache(IMemoryCache cache) => _cache = cache;

    public Task<SpendingSummaryResponse?> GetAsync(Guid customerId, int year, int month, CancellationToken ct = default)
    {
        _cache.TryGetValue(Key(customerId, year, month), out SpendingSummaryResponse? cached);
        return Task.FromResult(cached);
    }

    public Task SetAsync(Guid customerId, int year, int month, SpendingSummaryResponse response, CancellationToken ct = default)
    {
        var options = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(Ttl)
            .SetSize(1);  // each entry counts as 1 unit toward the cache size limit

        _cache.Set(Key(customerId, year, month), response, options);
        return Task.CompletedTask;
    }

    public Task InvalidateAsync(Guid customerId, int year, int month, CancellationToken ct = default)
    {
        _cache.Remove(Key(customerId, year, month));
        return Task.CompletedTask;
    }

    // Namespaced key — prevents collisions if multiple services share a cache
    private static string Key(Guid customerId, int year, int month) =>
        $"spending-api:summary:{customerId}:{year}:{month}";
}
