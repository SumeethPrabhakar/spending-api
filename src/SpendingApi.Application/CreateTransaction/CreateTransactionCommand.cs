namespace SpendingApi.Application.CreateTransaction;

public sealed record CreateTransactionCommand(
    Guid CustomerId,
    decimal Amount,
    string Currency,
    string MerchantName,
    Guid CategoryId,
    DateTime TransactionDate);

public sealed record CreateTransactionResponse(
    Guid TransactionId,
    Guid CustomerId,
    decimal Amount,
    string Currency,
    string MerchantName,
    Guid CategoryId,
    string Status,
    DateTime TransactionDate,
    DateTime CreatedAt);
