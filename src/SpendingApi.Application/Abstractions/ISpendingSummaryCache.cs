using SpendingApi.Application.SpendingSummary;

namespace SpendingApi.Application.Abstractions;

public interface ISpendingSummaryCache
{
    Task<SpendingSummaryResponse?> GetAsync(Guid customerId, int year, int month, CancellationToken ct = default);
    Task SetAsync(Guid customerId, int year, int month, SpendingSummaryResponse response, CancellationToken ct = default);
    Task InvalidateAsync(Guid customerId, int year, int month, CancellationToken ct = default);
}
