using SpendingApi.Api.RateLimiting;
using SpendingApi.Application.CreateTransaction;
using SpendingApi.Application.SpendingSummary;

namespace SpendingApi.Api.Endpoints;

public static class SpendingSummaryEndpoints
{
    public static void MapSpendingSummaryEndpoints(this WebApplication app)
    {
        app.MapPost("/api/v1/customers/{customerId:guid}/transactions", async (
            Guid customerId,
            CreateTransactionRequest request,
            CreateTransactionHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var authenticatedCustomerId = GetCustomerIdFromToken(httpContext);
            if (authenticatedCustomerId != customerId)
                return Results.Problem(
                    detail: "You are not authorised to create transactions for this customer.",
                    statusCode: StatusCodes.Status403Forbidden);

            var command = new CreateTransactionCommand(
                customerId,
                request.Amount,
                request.Currency,
                request.MerchantName,
                request.CategoryId,
                request.TransactionDate);

            var result = await handler.HandleAsync(command, cancellationToken);

            return result.IsFailure
                ? Results.Problem(detail: result.Error.Message, statusCode: StatusCodes.Status400BadRequest)
                : Results.Created($"/api/v1/customers/{customerId}/transactions/{result.Value.TransactionId}", result.Value);
        })
        .WithName("CreateTransaction")
        .WithTags("Transactions")
        .RequireAuthorization()
        .RequireRateLimiting(RateLimitingExtensions.PerCustomerPolicy)
        .Produces<CreateTransactionResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status429TooManyRequests)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        app.MapGet("/api/v1/customers/{customerId:guid}/spending-summary", async (
            Guid customerId,
            int year,
            int month,
            SpendingSummaryHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var authenticatedCustomerId = GetCustomerIdFromToken(httpContext);
            if (authenticatedCustomerId != customerId)
                return Results.Problem(
                    detail: "You are not authorised to view this customer's data.",
                    statusCode: StatusCodes.Status403Forbidden);

            var query = new GetSpendingSummaryQuery(customerId, year, month);
            var result = await handler.HandleAsync(query, cancellationToken);

            return result.IsFailure
                ? Results.Problem(detail: result.Error.Message, statusCode: StatusCodes.Status400BadRequest)
                : Results.Ok(result.Value);
        })
        .WithName("GetSpendingSummary")
        .WithTags("Spending")
        .RequireAuthorization()
        .RequireRateLimiting(RateLimitingExtensions.PerCustomerPolicy)
        .Produces<SpendingSummaryResponse>()
        .ProducesProblem(StatusCodes.Status429TooManyRequests)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status401Unauthorized);
    }

    private static Guid GetCustomerIdFromToken(HttpContext httpContext)
    {
        var sub = httpContext.User.FindFirst("sub")?.Value;
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }
}
