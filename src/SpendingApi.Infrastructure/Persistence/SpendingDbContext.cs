using Microsoft.EntityFrameworkCore;
using SpendingApi.Domain.Entities;

namespace SpendingApi.Infrastructure.Persistence;

public sealed class SpendingDbContext : DbContext
{
    public SpendingDbContext(DbContextOptions<SpendingDbContext> options) : base(options) { }

    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Category> Categories => Set<Category>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SpendingDbContext).Assembly);
    }
}
