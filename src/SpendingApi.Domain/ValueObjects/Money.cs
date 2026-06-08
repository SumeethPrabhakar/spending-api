using SpendingApi.Domain.Primitives;

namespace SpendingApi.Domain.ValueObjects;

public sealed record Money
{
    public decimal Amount { get; init; }
    public string Currency { get; init; }

    // Required by EF Core owned-type materialisation — never called by application code
    private Money() { Currency = null!; }

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public static Result<Money> Create(decimal amount, string currency = "AUD")
    {
        if (amount < 0)
            return Error.Validation("Amount cannot be negative.");

        if (string.IsNullOrWhiteSpace(currency))
            return Error.Validation("Currency is required.");

        return new Money(amount, currency.ToUpperInvariant());
    }

    public Money Add(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException("Cannot add amounts in different currencies.");

        return new Money(Amount + other.Amount, Currency);
    }

    public override string ToString() => $"{Currency} {Amount:F2}";
}
