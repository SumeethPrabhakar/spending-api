namespace SpendingApi.Application.SpendingSummary;

public sealed record GetSpendingSummaryQuery(
    Guid CustomerId,
    int Year,
    int Month);
