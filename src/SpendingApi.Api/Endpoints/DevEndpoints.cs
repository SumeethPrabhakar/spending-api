using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SpendingApi.Infrastructure.Persistence;
using SpendingApi.Domain.Entities;

namespace SpendingApi.Api.Endpoints;

// Dev-only — not registered in production
public static class DevEndpoints
{
    private const string Key = "dev-signing-key-at-least-32-chars!!";

    public static void MapDevEndpoints(this WebApplication app)
    {
        app.MapGet("/dev/token/{customerId:guid}", (Guid customerId) =>
        {
            var claims = new[]
            {
                new Claim("sub", customerId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key));
            var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: "spending-api-dev",
                audience: "spending-api",
                claims: claims,
                expires: DateTime.UtcNow.AddHours(8),
                signingCredentials: credentials);

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            return Results.Ok(new
            {
                customerId,
                token = tokenString,
                usage = $"Authorization: Bearer {tokenString}"
            });
        })
        .WithTags("Dev")
        .WithSummary("Generate a dev JWT for testing — not available in production");

        app.MapPost("/dev/seed/{customerId:guid}", async (Guid customerId, SpendingDbContext db) =>
        {
            if (await db.Categories.AnyAsync())
                return Results.Ok(new { message = "Already seeded", customerId });

            var groceries  = Category.Create("Groceries",     "🛒").Value;
            var dining     = Category.Create("Dining Out",    "🍽").Value;
            var transport  = Category.Create("Transport",     "🚌").Value;
            var shopping   = Category.Create("Shopping",      "🛍").Value;
            var utilities  = Category.Create("Utilities",     "💡").Value;

            db.Categories.AddRange(groceries, dining, transport, shopping, utilities);

            var transactions = new[]
            {
                Transaction.Load(Guid.NewGuid(), customerId, Domain.ValueObjects.Money.Create(120.50m).Value, "Woolworths",        groceries.Id,  TransactionStatus.Settled,  new DateTime(2026,6,1, 0,0,0, DateTimeKind.Utc),  DateTime.UtcNow, DateTime.UtcNow, null),
                Transaction.Load(Guid.NewGuid(), customerId, Domain.ValueObjects.Money.Create(85.00m).Value,  "Coles",             groceries.Id,  TransactionStatus.Settled,  new DateTime(2026,6,5, 0,0,0, DateTimeKind.Utc),  DateTime.UtcNow, DateTime.UtcNow, null),
                Transaction.Load(Guid.NewGuid(), customerId, Domain.ValueObjects.Money.Create(42.00m).Value,  "McDonald's",        dining.Id,     TransactionStatus.Settled,  new DateTime(2026,6,3, 0,0,0, DateTimeKind.Utc),  DateTime.UtcNow, DateTime.UtcNow, null),
                Transaction.Load(Guid.NewGuid(), customerId, Domain.ValueObjects.Money.Create(95.00m).Value,  "Restaurant Hubert", dining.Id,     TransactionStatus.Settled,  new DateTime(2026,6,7, 0,0,0, DateTimeKind.Utc),  DateTime.UtcNow, DateTime.UtcNow, null),
                Transaction.Load(Guid.NewGuid(), customerId, Domain.ValueObjects.Money.Create(55.20m).Value,  "Opal Card",         transport.Id,  TransactionStatus.Settled,  new DateTime(2026,6,2, 0,0,0, DateTimeKind.Utc),  DateTime.UtcNow, DateTime.UtcNow, null),
                Transaction.Load(Guid.NewGuid(), customerId, Domain.ValueObjects.Money.Create(220.00m).Value, "JB Hi-Fi",          shopping.Id,   TransactionStatus.Settled,  new DateTime(2026,6,4, 0,0,0, DateTimeKind.Utc),  DateTime.UtcNow, DateTime.UtcNow, null),
                Transaction.Load(Guid.NewGuid(), customerId, Domain.ValueObjects.Money.Create(180.00m).Value, "AGL Energy",        utilities.Id,  TransactionStatus.Settled,  new DateTime(2026,6,6, 0,0,0, DateTimeKind.Utc),  DateTime.UtcNow, DateTime.UtcNow, null),
            };

            db.Transactions.AddRange(transactions);
            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                message  = "Seeded successfully",
                customerId,
                categories = 5,
                transactions = transactions.Length,
                hint = $"Now call GET /api/v1/customers/{customerId}/spending-summary?year=2026&month=6"
            });
        })
        .WithTags("Dev")
        .WithSummary("Seed categories and sample transactions for a customer — not available in production");
    }
}
