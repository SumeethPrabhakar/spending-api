namespace SpendingApi.Application.Abstractions;

public interface IIdempotencyStore
{
    Task<IdempotencyResult?> GetAsync(string key, CancellationToken cancellationToken = default);
    Task StoreAsync(string key, IdempotencyResult result, CancellationToken cancellationToken = default);
}

public sealed record IdempotencyResult(
    int StatusCode,
    string ContentType,
    string Body,
    DateTimeOffset CreatedAt);
