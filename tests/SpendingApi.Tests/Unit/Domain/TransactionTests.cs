using SpendingApi.Domain.Entities;
using SpendingApi.Domain.Events;
using SpendingApi.Domain.ValueObjects;

namespace SpendingApi.Tests.Unit.Domain;

public sealed class TransactionTests
{
    private static readonly Money ValidAmount = Money.Create(50m).Value;
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid CategoryId = Guid.NewGuid();

    // ── Create ──────────────────────────────────────────────────────────────

    [Fact]
    public void Create_WithValidData_Succeeds()
    {
        var result = Transaction.Create(CustomerId, ValidAmount, "Woolworths", CategoryId, DateTime.UtcNow);

        Assert.True(result.IsSuccess);
        Assert.Equal(CustomerId, result.Value.CustomerId);
        Assert.Equal("Woolworths", result.Value.MerchantName);
        Assert.Equal(TransactionStatus.Pending, result.Value.Status);
    }

    [Fact]
    public void Create_RaisesTransactionCreatedEvent()
    {
        var result = Transaction.Create(CustomerId, ValidAmount, "Woolworths", CategoryId, DateTime.UtcNow);

        Assert.Single(result.Value.DomainEvents);
        Assert.IsType<TransactionCreatedEvent>(result.Value.DomainEvents[0]);
    }

    [Fact]
    public void Create_WithEmptyCustomerId_Fails()
    {
        var result = Transaction.Create(Guid.Empty, ValidAmount, "Woolworths", CategoryId, DateTime.UtcNow);

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION", result.Error.Code);
    }

    [Fact]
    public void Create_WithEmptyMerchantName_Fails()
    {
        var result = Transaction.Create(CustomerId, ValidAmount, "", CategoryId, DateTime.UtcNow);

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION", result.Error.Code);
    }

    [Fact]
    public void Create_WithWhitespaceMerchantName_Fails()
    {
        var result = Transaction.Create(CustomerId, ValidAmount, "   ", CategoryId, DateTime.UtcNow);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Create_TrimsWhitespaceFromMerchantName()
    {
        var result = Transaction.Create(CustomerId, ValidAmount, "  Coles  ", CategoryId, DateTime.UtcNow);

        Assert.True(result.IsSuccess);
        Assert.Equal("Coles", result.Value.MerchantName);
    }

    [Fact]
    public void Create_AssignsNewGuidId()
    {
        var a = Transaction.Create(CustomerId, ValidAmount, "Merchant", CategoryId, DateTime.UtcNow).Value;
        var b = Transaction.Create(CustomerId, ValidAmount, "Merchant", CategoryId, DateTime.UtcNow).Value;

        Assert.NotEqual(a.Id, b.Id);
    }

    // ── Settle ──────────────────────────────────────────────────────────────

    [Fact]
    public void Settle_PendingTransaction_ChangesStatusToSettled()
    {
        var transaction = Transaction.Create(CustomerId, ValidAmount, "Merchant", CategoryId, DateTime.UtcNow).Value;

        var result = transaction.Settle();

        Assert.True(result.IsSuccess);
        Assert.Equal(TransactionStatus.Settled, transaction.Status);
    }

    [Fact]
    public void Settle_AlreadySettled_ReturnsConflictError()
    {
        var transaction = Transaction.Create(CustomerId, ValidAmount, "Merchant", CategoryId, DateTime.UtcNow).Value;
        transaction.Settle();

        var result = transaction.Settle();

        Assert.True(result.IsFailure);
        Assert.Equal("CONFLICT", result.Error.Code);
    }

    [Fact]
    public void Settle_ReversedTransaction_ReturnsConflictError()
    {
        var transaction = Transaction.Create(CustomerId, ValidAmount, "Merchant", CategoryId, DateTime.UtcNow).Value;
        transaction.Reverse();

        var result = transaction.Settle();

        Assert.True(result.IsFailure);
        Assert.Equal("CONFLICT", result.Error.Code);
    }

    // ── Reverse ─────────────────────────────────────────────────────────────

    [Fact]
    public void Reverse_PendingTransaction_ChangesStatusToReversed()
    {
        var transaction = Transaction.Create(CustomerId, ValidAmount, "Merchant", CategoryId, DateTime.UtcNow).Value;

        var result = transaction.Reverse();

        Assert.True(result.IsSuccess);
        Assert.Equal(TransactionStatus.Reversed, transaction.Status);
    }

    [Fact]
    public void Reverse_AlreadyReversed_ReturnsConflictError()
    {
        var transaction = Transaction.Create(CustomerId, ValidAmount, "Merchant", CategoryId, DateTime.UtcNow).Value;
        transaction.Reverse();

        var result = transaction.Reverse();

        Assert.True(result.IsFailure);
        Assert.Equal("CONFLICT", result.Error.Code);
    }

    // ── SoftDelete ──────────────────────────────────────────────────────────

    [Fact]
    public void SoftDelete_SetsDeletedAt()
    {
        var transaction = Transaction.Create(CustomerId, ValidAmount, "Merchant", CategoryId, DateTime.UtcNow).Value;

        transaction.SoftDelete();

        Assert.True(transaction.IsDeleted);
        Assert.NotNull(transaction.DeletedAt);
    }

    [Fact]
    public void IsDeleted_BeforeSoftDelete_ReturnsFalse()
    {
        var transaction = Transaction.Create(CustomerId, ValidAmount, "Merchant", CategoryId, DateTime.UtcNow).Value;

        Assert.False(transaction.IsDeleted);
    }

    // ── Domain Events ────────────────────────────────────────────────────────

    [Fact]
    public void ClearDomainEvents_RemovesAllEvents()
    {
        var transaction = Transaction.Create(CustomerId, ValidAmount, "Merchant", CategoryId, DateTime.UtcNow).Value;

        transaction.ClearDomainEvents();

        Assert.Empty(transaction.DomainEvents);
    }

    [Fact]
    public void Load_DoesNotRaiseDomainEvents()
    {
        var transaction = Transaction.Load(
            Guid.NewGuid(), CustomerId, ValidAmount, "Merchant", CategoryId,
            TransactionStatus.Settled, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow, null);

        Assert.Empty(transaction.DomainEvents);
    }
}
