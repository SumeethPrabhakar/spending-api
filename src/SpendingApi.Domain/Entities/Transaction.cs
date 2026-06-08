using SpendingApi.Domain.Events;
using SpendingApi.Domain.Primitives;
using SpendingApi.Domain.ValueObjects;

namespace SpendingApi.Domain.Entities;

public sealed class Transaction
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public Money Amount { get; private set; }
    public string MerchantName { get; private set; }
    public Guid CategoryId { get; private set; }
    public TransactionStatus Status { get; private set; }
    public DateTime TransactionDate { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    // Required by EF Core to materialise entities — never called by application code
    private Transaction()
    {
        MerchantName = null!;
        Amount = null!;
    }

    private Transaction(
        Guid id,
        Guid customerId,
        Money amount,
        string merchantName,
        Guid categoryId,
        DateTime transactionDate)
    {
        Id = id;
        CustomerId = customerId;
        Amount = amount;
        MerchantName = merchantName;
        CategoryId = categoryId;
        Status = TransactionStatus.Pending;
        TransactionDate = transactionDate;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public static Result<Transaction> Create(
        Guid customerId,
        Money amount,
        string merchantName,
        Guid categoryId,
        DateTime transactionDate)
    {
        if (customerId == Guid.Empty)
            return Error.Validation("Customer ID is required.");

        if (string.IsNullOrWhiteSpace(merchantName))
            return Error.Validation("Merchant name is required.");

        if (categoryId == Guid.Empty)
            return Error.Validation("Category ID is required.");

        var transaction = new Transaction(
            Guid.NewGuid(),
            customerId,
            amount,
            merchantName.Trim(),
            categoryId,
            transactionDate);

        transaction._domainEvents.Add(new TransactionCreatedEvent(transaction.Id, customerId));

        return transaction;
    }

    public Result<Transaction> Settle()
    {
        if (Status == TransactionStatus.Settled)
            return Error.Conflict("Transaction is already settled.");

        if (Status == TransactionStatus.Reversed)
            return Error.Conflict("Cannot settle a reversed transaction.");

        Status = TransactionStatus.Settled;
        UpdatedAt = DateTime.UtcNow;
        return this;
    }

    public Result<Transaction> Reverse()
    {
        if (Status == TransactionStatus.Reversed)
            return Error.Conflict("Transaction is already reversed.");

        Status = TransactionStatus.Reversed;
        UpdatedAt = DateTime.UtcNow;
        return this;
    }

    public static Transaction Load(
        Guid id,
        Guid customerId,
        Money amount,
        string merchantName,
        Guid categoryId,
        TransactionStatus status,
        DateTime transactionDate,
        DateTime createdAt,
        DateTime updatedAt,
        DateTime? deletedAt)
    {
        return new Transaction(id, customerId, amount, merchantName, categoryId, transactionDate)
        {
            Status = status,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            DeletedAt = deletedAt
        };
    }

    public void SoftDelete() => DeletedAt = DateTime.UtcNow;

    public void ClearDomainEvents() => _domainEvents.Clear();

    public bool IsDeleted => DeletedAt.HasValue;
}

public enum TransactionStatus
{
    Pending,
    Settled,
    Reversed
}
