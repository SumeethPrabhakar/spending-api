using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace SpendingApi.Api.RateLimiting;

public static class RateLimitingExtensions
{
    public const string PerCustomerPolicy = "per-customer";

    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            // Per-customer sliding window — 60 requests per minute
            // Partitioned by JWT sub claim (not IP — IPs are shared in corporate networks)
            options.AddPolicy<string, CustomerRateLimitPolicy>(PerCustomerPolicy);

            options.OnRejected = async (ctx, ct) =>
            {
                ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                if (ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    ctx.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString();

                await ctx.HttpContext.Response.WriteAsJsonAsync(new
                {
                    type = "https://tools.ietf.org/html/rfc6585#section-4",
                    title = "Too Many Requests",
                    status = 429,
                    detail = "Rate limit exceeded. See Retry-After header."
                }, ct);
            };
        });

        return services;
    }
}

// Separate class makes the policy reusable and testable
public sealed class CustomerRateLimitPolicy : IRateLimiterPolicy<string>
{
    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        // Authenticated: partition by customer ID from JWT
        // Unauthenticated: partition by IP (will fail auth anyway, but still protect the server)
        var partitionKey =
            httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? httpContext.User.FindFirstValue("sub")
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous";

        return RateLimitPartition.GetSlidingWindowLimiter(partitionKey, _ => new SlidingWindowRateLimiterOptions
        {
            Window = TimeSpan.FromMinutes(1),
            SegmentsPerWindow = 6,       // 6 × 10s segments — smooth sliding, O(1) memory
            PermitLimit = 60,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0               // reject immediately — no queuing for financial APIs
        });
    }

    // Called if the policy throws — return null to re-throw, return a lease to handle gracefully
    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => null;
}
