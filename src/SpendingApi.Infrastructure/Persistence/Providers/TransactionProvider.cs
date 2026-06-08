using System.Data;
using Dapper;
using Polly;
using Polly.Retry;
using SpendingApi.Application.Abstractions;
using SpendingApi.Domain.Entities;
using SpendingApi.Domain.ValueObjects;

namespace SpendingApi.Infrastructure.Persistence.Providers;

public sealed class TransactionProvider : ITransactionProvider
{
    private readonly IDbConnection _connection;

    // Static — built once, shared across all scoped instances
    // Retry 3× on transient errors with exponential backoff + jitter, 10s timeout per attempt
    private static readonly ResiliencePipeline Pipeline = new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromMilliseconds(200),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            ShouldHandle = new PredicateBuilder()
                .Handle<TimeoutException>()
                .Handle<InvalidOperationException>()  // covers closed connection retries
        })
        .AddTimeout(TimeSpan.FromSeconds(10))
        .Build();

    public TransactionProvider(IDbConnection connection) => _connection = connection;

    public async Task<IReadOnlyList<Transaction>> GetByCustomerAndMonthAsync(
        Guid customerId,
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT t.id,
                   t.customer_id   AS customerid,
                   t.amount,
                   t.currency,
                   t.merchant_name AS merchantname,
                   t.category_id   AS categoryid,
                   t.status,
                   t.transaction_date AS transactiondate,
                   t.created_at    AS createdat,
                   t.updated_at    AS updatedat,
                   t.deleted_at    AS deletedat
            FROM transactions t
            INNER JOIN categories c ON c.id = t.category_id
            WHERE t.customer_id = @CustomerId
              AND EXTRACT(YEAR FROM t.transaction_date) = @Year
              AND EXTRACT(MONTH FROM t.transaction_date) = @Month
              AND t.deleted_at IS NULL
              AND c.deleted_at IS NULL
            ORDER BY t.transaction_date DESC
            """;

        // Resilience pipeline: retry on transient errors, hard timeout at 10 seconds
        return await Pipeline.ExecuteAsync(async ct =>
        {
            var rows = await _connection.QueryAsync<TransactionRow>(
                sql, new { CustomerId = customerId, Year = year, Month = month });

            return (IReadOnlyList<Transaction>)rows.Select(MapToDomain).ToList();
        }, cancellationToken);
    }

    private static Transaction MapToDomain(TransactionRow row)
    {
        var money = Money.Create(row.Amount, row.Currency).Value;
        return Transaction.Load(
            row.Id,
            row.CustomerId,
            money,
            row.MerchantName,
            row.CategoryId,
            Enum.Parse<TransactionStatus>(row.Status),
            row.TransactionDate,
            row.CreatedAt,
            row.UpdatedAt,
            row.DeletedAt);
    }

    private sealed record TransactionRow(
        Guid Id,
        Guid CustomerId,
        decimal Amount,
        string Currency,
        string MerchantName,
        Guid CategoryId,
        string Status,
        DateTime TransactionDate,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        DateTime? DeletedAt);
}
