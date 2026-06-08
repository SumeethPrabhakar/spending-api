namespace SpendingApi.Api.Endpoints;

public sealed record CreateTransactionRequest(
    decimal Amount,
    string Currency,
    string MerchantName,
    Guid CategoryId,
    DateTime TransactionDate);
