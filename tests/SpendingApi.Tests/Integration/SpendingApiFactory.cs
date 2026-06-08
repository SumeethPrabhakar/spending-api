using System.Data;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using SpendingApi.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace SpendingApi.Tests.Integration;

public sealed class SpendingApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:15-alpine")
        .WithDatabase("spending_api_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Replace EF Core DbContext with test container
            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<SpendingDbContext>));
            if (dbContextDescriptor is not null) services.Remove(dbContextDescriptor);
            services.AddDbContext<SpendingDbContext>(options =>
                options.UseNpgsql(_postgres.GetConnectionString()));

            // Replace Dapper IDbConnection with test container connection
            var dbConnectionDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IDbConnection));
            if (dbConnectionDescriptor is not null) services.Remove(dbConnectionDescriptor);
            services.AddScoped<IDbConnection>(_ => new NpgsqlConnection(_postgres.GetConnectionString()));

            // Override JWT to validate tokens signed with the test key
            services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.Authority = null;
                options.RequireHttpsMetadata = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = "spending-api-test",
                    ValidateAudience = true,
                    ValidAudience = "spending-api",
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes("test-signing-key-at-least-32-chars!!")),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
            });
        });
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendingDbContext>();
        await db.Database.MigrateAsync();
    }

    public new async Task DisposeAsync() => await _postgres.StopAsync();
}
