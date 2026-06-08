using SpendingApi.Domain.Entities;

namespace SpendingApi.Application.Abstractions;

public interface ITransactionProvider
{
    Task<IReadOnlyList<Transaction>> GetByCustomerAndMonthAsync(
        Guid customerId,
        int year,
        int month,
        CancellationToken cancellationToken = default);
}
