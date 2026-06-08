using SpendingApi.Application.Abstractions;
using SpendingApi.Domain.Entities;
using SpendingApi.Domain.Primitives;
using SpendingApi.Domain.ValueObjects;

namespace SpendingApi.Application.CreateTransaction;

public sealed class CreateTransactionHandler
{
    private readonly IRepository<Transaction> _transactionRepository;
    private readonly IReadOnlyRepository<Category> _categoryRepository;
    private readonly IEventDispatcher _eventDispatcher;
    private readonly ISpendingSummaryCache _summaryCache;

    public CreateTransactionHandler(
        IRepository<Transaction> transactionRepository,
        IReadOnlyRepository<Category> categoryRepository,
        IEventDispatcher eventDispatcher,
        ISpendingSummaryCache summaryCache)
    {
        _transactionRepository = transactionRepository;
        _categoryRepository = categoryRepository;
        _eventDispatcher = eventDispatcher;
        _summaryCache = summaryCache;
    }

    public async Task<Result<CreateTransactionResponse>> HandleAsync(
        CreateTransactionCommand command,
        CancellationToken cancellationToken = default)
    {
        // Validate category exists
        var categoryResult = await _categoryRepository.GetByIdAsync(command.CategoryId, cancellationToken);
        if (categoryResult.IsFailure)
            return Error.NotFound($"Category '{command.CategoryId}' not found.");

        // Create Money value object — validates amount and currency
        var moneyResult = Money.Create(command.Amount, command.Currency);
        if (moneyResult.IsFailure)
            return moneyResult.Error;

        // Create Transaction via rich domain model — raises TransactionCreatedEvent
        var transactionResult = Transaction.Create(
            command.CustomerId,
            moneyResult.Value,
            command.MerchantName,
            command.CategoryId,
            command.TransactionDate);

        if (transactionResult.IsFailure)
            return transactionResult.Error;

        var transaction = transactionResult.Value;

        await _transactionRepository.SaveAsync(transaction, cancellationToken);

        // Dispatch domain events after persistence
        foreach (var domainEvent in transaction.DomainEvents)
            await _eventDispatcher.DispatchAsync(domainEvent, cancellationToken);
        transaction.ClearDomainEvents();

        // Invalidate the spending summary cache for this customer/month so the next
        // GET returns fresh data including this new transaction
        await _summaryCache.InvalidateAsync(
            command.CustomerId,
            command.TransactionDate.Year,
            command.TransactionDate.Month,
            cancellationToken);

        return new CreateTransactionResponse(
            TransactionId: transaction.Id,
            CustomerId: transaction.CustomerId,
            Amount: transaction.Amount.Amount,
            Currency: transaction.Amount.Currency,
            MerchantName: transaction.MerchantName,
            CategoryId: transaction.CategoryId,
            Status: transaction.Status.ToString(),
            TransactionDate: transaction.TransactionDate,
            CreatedAt: transaction.CreatedAt);
    }
}
