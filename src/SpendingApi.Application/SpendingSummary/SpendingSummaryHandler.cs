using SpendingApi.Application.Abstractions;
using SpendingApi.Domain.Entities;
using SpendingApi.Domain.Primitives;

namespace SpendingApi.Application.SpendingSummary;

public sealed class SpendingSummaryHandler
{
    private readonly ITransactionProvider _transactionProvider;
    private readonly IReadOnlyRepository<Category> _categoryRepository;
    private readonly ISpendingSummaryCache _cache;

    public SpendingSummaryHandler(
        ITransactionProvider transactionProvider,
        IReadOnlyRepository<Category> categoryRepository,
        ISpendingSummaryCache cache)
    {
        _transactionProvider = transactionProvider;
        _categoryRepository = categoryRepository;
        _cache = cache;
    }

    public async Task<Result<SpendingSummaryResponse>> HandleAsync(
        GetSpendingSummaryQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.Month < 1 || query.Month > 12)
            return Error.Validation("Month must be between 1 and 12.");

        if (query.Year < 2000 || query.Year > DateTime.UtcNow.Year)
            return Error.Validation("Invalid year.");

        // Cache-aside: check cache before hitting the DB
        var cached = await _cache.GetAsync(query.CustomerId, query.Year, query.Month, cancellationToken);
        if (cached is not null)
            return cached;

        var transactions = await _transactionProvider.GetByCustomerAndMonthAsync(
            query.CustomerId,
            query.Year,
            query.Month,
            cancellationToken);

        var categories = await _categoryRepository.GetAllAsync(cancellationToken);
        var categoryLookup = categories.ToDictionary(c => c.Id);

        var grouped = transactions
            .GroupBy(t => t.CategoryId)
            .Select(g =>
            {
                categoryLookup.TryGetValue(g.Key, out var category);
                return new CategorySummary(
                    CategoryId: g.Key,
                    CategoryName: category?.Name ?? "Uncategorised",
                    Icon: category?.Icon ?? "",
                    TotalSpend: g.Sum(t => t.Amount.Amount),
                    TransactionCount: g.Count());
            })
            .OrderByDescending(c => c.TotalSpend)
            .ToList();

        var response = new SpendingSummaryResponse(
            CustomerId: query.CustomerId,
            Year: query.Year,
            Month: query.Month,
            TotalSpend: grouped.Sum(c => c.TotalSpend),
            Currency: "AUD",
            Categories: grouped);

        // Store result — invalidated when a new transaction is created for this customer/month
        await _cache.SetAsync(query.CustomerId, query.Year, query.Month, response, cancellationToken);

        return response;
    }
}
