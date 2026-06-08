using SpendingApi.Application.Abstractions;
using SpendingApi.Domain.Entities;

namespace SpendingApi.Application.SpendingSummary;

public sealed class TransactionsByCustomerAndMonthSpec : BaseSpecification<Transaction>
{
    private readonly Guid _customerId;
    private readonly int _year;
    private readonly int _month;

    public TransactionsByCustomerAndMonthSpec(Guid customerId, int year, int month)
        : base(t => t.CustomerId == customerId
                 && t.TransactionDate.Year == year
                 && t.TransactionDate.Month == month
                 && t.DeletedAt == null)
    {
        _customerId = customerId;
        _year = year;
        _month = month;
        ApplyOrderByDescending(t => t.TransactionDate);
    }

    public override object? GetParameters() => new
    {
        CustomerId = _customerId,
        Year = _year,
        Month = _month
    };
}
