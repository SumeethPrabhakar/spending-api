namespace SpendingApi.Application.SpendingSummary;

public sealed record SpendingSummaryResponse(
    Guid CustomerId,
    int Year,
    int Month,
    decimal TotalSpend,
    string Currency,
    IReadOnlyList<CategorySummary> Categories);

public sealed record CategorySummary(
    Guid CategoryId,
    string CategoryName,
    string Icon,
    decimal TotalSpend,
    int TransactionCount);
