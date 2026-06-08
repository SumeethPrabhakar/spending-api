using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SpendingApi.Application.Abstractions;
using SpendingApi.Domain.Entities;
using SpendingApi.Infrastructure.Caching;
using SpendingApi.Infrastructure.Idempotency;
using SpendingApi.Infrastructure.Messaging;
using SpendingApi.Infrastructure.Persistence;
using SpendingApi.Infrastructure.Persistence.Providers;
using SpendingApi.Infrastructure.Persistence.Repositories;

namespace SpendingApi.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        // EF Core — for writes
        services.AddDbContext<SpendingDbContext>(options =>
            options.UseNpgsql(connectionString));

        // Dapper — for reads
        services.AddScoped<IDbConnection>(_ => new NpgsqlConnection(connectionString));

        // Repositories
        services.AddScoped<IRepository<Transaction>, TransactionRepository>();
        services.AddScoped<IReadOnlyRepository<Category>, CategoryRepository>();

        // Providers — Dapper complex reads (resilience pipeline built into TransactionProvider)
        services.AddScoped<ITransactionProvider, TransactionProvider>();

        // Messaging
        services.AddScoped<IEventDispatcher, EventDispatcher>();

        // Idempotency — swap for Redis-backed store in production
        services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();

        // Caching — swap for Redis-backed implementation in production
        // SizeLimit prevents unbounded growth; each cached summary = 1 unit
        services.AddMemoryCache(opts => opts.SizeLimit = 1000);
        services.AddSingleton<ISpendingSummaryCache, InMemorySpendingSummaryCache>();

        return services;
    }
}
