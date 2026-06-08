using NSubstitute;
using SpendingApi.Application.Abstractions;
using SpendingApi.Application.SpendingSummary;
using SpendingApi.Domain.Entities;
using SpendingApi.Domain.ValueObjects;

namespace SpendingApi.Tests.Unit.Application;

public sealed class SpendingSummaryHandlerTests
{
    private readonly ITransactionProvider _provider = Substitute.For<ITransactionProvider>();
    private readonly IReadOnlyRepository<Category> _categoryRepo = Substitute.For<IReadOnlyRepository<Category>>();
    private readonly ISpendingSummaryCache _cache = Substitute.For<ISpendingSummaryCache>();
    private readonly SpendingSummaryHandler _handler;

    public SpendingSummaryHandlerTests()
    {
        _handler = new SpendingSummaryHandler(_provider, _categoryRepo, _cache);
    }

    // ── Validation ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(-1)]
    public async Task HandleAsync_InvalidMonth_ReturnsValidationError(int month)
    {
        var query = new GetSpendingSummaryQuery(Guid.NewGuid(), 2026, month);

        var result = await _handler.HandleAsync(query);

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION", result.Error.Code);
    }

    [Fact]
    public async Task HandleAsync_FutureYear_ReturnsValidationError()
    {
        var query = new GetSpendingSummaryQuery(Guid.NewGuid(), DateTime.UtcNow.Year + 1, 1);

        var result = await _handler.HandleAsync(query);

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION", result.Error.Code);
    }

    // ── Cache ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_CacheHit_ReturnsCachedResponseWithoutCallingProvider()
    {
        var customerId = Guid.NewGuid();
        var cached = new SpendingSummaryResponse(customerId, 2026, 1, 100m, "AUD", []);
        var query = new GetSpendingSummaryQuery(customerId, 2026, 1);

        _cache.GetAsync(customerId, 2026, 1).Returns(cached);

        var result = await _handler.HandleAsync(query);

        Assert.True(result.IsSuccess);
        Assert.Equal(cached, result.Value);

        // provider must NOT be called on a cache hit
        await _provider.Received(0).GetByCustomerAndMonthAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<int>());
    }

    [Fact]
    public async Task HandleAsync_CacheMiss_StoresResultInCacheAfterLoad()
    {
        var customerId = Guid.NewGuid();
        var query = new GetSpendingSummaryQuery(customerId, 2026, 1);

        _cache.GetAsync(customerId, 2026, 1).Returns((SpendingSummaryResponse?)null);
        _provider.GetByCustomerAndMonthAsync(customerId, 2026, 1).Returns([]);
        _categoryRepo.GetAllAsync().Returns([]);

        await _handler.HandleAsync(query);

        await _cache.Received(1).SetAsync(customerId, 2026, 1, Arg.Any<SpendingSummaryResponse>());
    }

    // ── Grouping ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_GroupsTransactionsByCategory()
    {
        var customerId = Guid.NewGuid();
        var query = new GetSpendingSummaryQuery(customerId, 2026, 1);

        // Create categories first — use their assigned IDs for transactions
        var groceries = Category.Create("Groceries", "🛒").Value;
        var dining    = Category.Create("Dining",    "🍽").Value;

        var transactions = new List<Transaction>
        {
            Transaction.Load(Guid.NewGuid(), customerId, Money.Create(100m).Value, "Woolworths", groceries.Id, TransactionStatus.Settled, new DateTime(2026,1,10), DateTime.UtcNow, DateTime.UtcNow, null),
            Transaction.Load(Guid.NewGuid(), customerId, Money.Create(50m).Value,  "Coles",      groceries.Id, TransactionStatus.Settled, new DateTime(2026,1,15), DateTime.UtcNow, DateTime.UtcNow, null),
            Transaction.Load(Guid.NewGuid(), customerId, Money.Create(30m).Value,  "McDonald's", dining.Id,    TransactionStatus.Settled, new DateTime(2026,1,20), DateTime.UtcNow, DateTime.UtcNow, null),
        };

        _cache.GetAsync(customerId, 2026, 1).Returns((SpendingSummaryResponse?)null);
        _provider.GetByCustomerAndMonthAsync(customerId, 2026, 1).Returns(transactions);
        _categoryRepo.GetAllAsync().Returns(new List<Category> { groceries, dining });

        var result = await _handler.HandleAsync(query);

        Assert.True(result.IsSuccess);
        Assert.Equal(180m, result.Value.TotalSpend);
        Assert.Equal(2, result.Value.Categories.Count);

        var groceriesSummary = result.Value.Categories.First(c => c.CategoryName == "Groceries");
        Assert.Equal(150m, groceriesSummary.TotalSpend);
        Assert.Equal(2, groceriesSummary.TransactionCount);

        var diningSummary = result.Value.Categories.First(c => c.CategoryName == "Dining");
        Assert.Equal(30m, diningSummary.TotalSpend);
        Assert.Equal(1, diningSummary.TransactionCount);
    }

    [Fact]
    public async Task HandleAsync_OrdersCategoriesByHighestSpendFirst()
    {
        var customerId = Guid.NewGuid();
        var catA = Category.Create("Low spend", "A").Value;
        var catB = Category.Create("High spend", "B").Value;
        var query = new GetSpendingSummaryQuery(customerId, 2026, 1);

        var transactions = new List<Transaction>
        {
            Transaction.Load(Guid.NewGuid(), customerId, Money.Create(10m).Value,  "A", catA.Id, TransactionStatus.Settled, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow, null),
            Transaction.Load(Guid.NewGuid(), customerId, Money.Create(999m).Value, "B", catB.Id, TransactionStatus.Settled, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow, null),
        };

        _cache.GetAsync(customerId, 2026, 1).Returns((SpendingSummaryResponse?)null);
        _provider.GetByCustomerAndMonthAsync(customerId, 2026, 1).Returns(transactions);
        _categoryRepo.GetAllAsync().Returns(new List<Category> { catA, catB });

        var result = await _handler.HandleAsync(query);

        Assert.Equal(999m, result.Value.Categories[0].TotalSpend);
        Assert.Equal(10m, result.Value.Categories[1].TotalSpend);
    }

    [Fact]
    public async Task HandleAsync_UnknownCategory_UsesUncategorisedFallback()
    {
        var customerId = Guid.NewGuid();
        var unknownCategoryId = Guid.NewGuid();
        var query = new GetSpendingSummaryQuery(customerId, 2026, 1);

        var transactions = new List<Transaction>
        {
            Transaction.Load(Guid.NewGuid(), customerId, Money.Create(50m).Value, "Merchant", unknownCategoryId, TransactionStatus.Settled, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow, null),
        };

        _cache.GetAsync(customerId, 2026, 1).Returns((SpendingSummaryResponse?)null);
        _provider.GetByCustomerAndMonthAsync(customerId, 2026, 1).Returns(transactions);
        _categoryRepo.GetAllAsync().Returns([]);  // no categories

        var result = await _handler.HandleAsync(query);

        Assert.Equal("Uncategorised", result.Value.Categories[0].CategoryName);
    }

    [Fact]
    public async Task HandleAsync_NoTransactions_ReturnsZeroTotalSpend()
    {
        var customerId = Guid.NewGuid();
        var query = new GetSpendingSummaryQuery(customerId, 2026, 1);

        _cache.GetAsync(customerId, 2026, 1).Returns((SpendingSummaryResponse?)null);
        _provider.GetByCustomerAndMonthAsync(customerId, 2026, 1).Returns([]);
        _categoryRepo.GetAllAsync().Returns([]);

        var result = await _handler.HandleAsync(query);

        Assert.True(result.IsSuccess);
        Assert.Equal(0m, result.Value.TotalSpend);
        Assert.Empty(result.Value.Categories);
    }
}
