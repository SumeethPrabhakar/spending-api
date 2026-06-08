using SpendingApi.Application.Abstractions;

namespace SpendingApi.Api.Middleware;

public sealed class IdempotencyMiddleware
{
    private const string IdempotencyKeyHeader = "Idempotency-Key";
    private readonly RequestDelegate _next;
    private readonly ILogger<IdempotencyMiddleware> _logger;

    public IdempotencyMiddleware(RequestDelegate next, ILogger<IdempotencyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IIdempotencyStore store)
    {
        // Only apply to write operations
        if (!IsWriteMethod(context.Request.Method))
        {
            await _next(context);
            return;
        }

        // No idempotency key — let it through (caller's responsibility)
        if (!context.Request.Headers.TryGetValue(IdempotencyKeyHeader, out var key) || string.IsNullOrWhiteSpace(key))
        {
            await _next(context);
            return;
        }

        var idempotencyKey = key.ToString();

        // Check if we've seen this key before
        var existing = await store.GetAsync(idempotencyKey, context.RequestAborted);
        if (existing is not null)
        {
            _logger.LogInformation(
                "Duplicate request detected for Idempotency-Key {Key} — returning cached response",
                idempotencyKey);

            context.Response.StatusCode = existing.StatusCode;
            context.Response.ContentType = existing.ContentType;
            await context.Response.WriteAsync(existing.Body, context.RequestAborted);
            return;
        }

        // Capture the response so we can store it
        var originalBody = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        await _next(context);

        // Only cache successful responses
        if (context.Response.StatusCode is >= 200 and < 300)
        {
            buffer.Seek(0, SeekOrigin.Begin);
            var responseBody = await new StreamReader(buffer).ReadToEndAsync(context.RequestAborted);

            var result = new IdempotencyResult(
                StatusCode: context.Response.StatusCode,
                ContentType: context.Response.ContentType ?? "application/json",
                Body: responseBody,
                CreatedAt: DateTimeOffset.UtcNow);

            await store.StoreAsync(idempotencyKey, result, context.RequestAborted);

            buffer.Seek(0, SeekOrigin.Begin);
        }

        // Write captured response back to original stream
        buffer.Seek(0, SeekOrigin.Begin);
        await buffer.CopyToAsync(originalBody, context.RequestAborted);
        context.Response.Body = originalBody;
    }

    private static bool IsWriteMethod(string method) =>
        method is "POST" or "PUT" or "PATCH";
}
