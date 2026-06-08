using Microsoft.EntityFrameworkCore;
using SpendingApi.Application.Abstractions;
using SpendingApi.Domain.Entities;
using SpendingApi.Domain.Primitives;

namespace SpendingApi.Infrastructure.Persistence.Repositories;

public sealed class TransactionRepository : IRepository<Transaction>
{
    private readonly SpendingDbContext _context;

    public TransactionRepository(SpendingDbContext context)
    {
        _context = context;
    }

    // Simple read — EF Core
    public async Task<Result<Transaction>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var transaction = await _context.Transactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (transaction is null)
            return Error.NotFound($"Transaction {id} not found.");

        return transaction;
    }

    public async Task<IReadOnlyList<Transaction>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Transactions
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public Task<IReadOnlyList<Transaction>> FindAsync(ISpecification<Transaction> specification, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Use TransactionProvider for complex queries.");

    // Writes — EF Core
    public async Task SaveAsync(Transaction entity, CancellationToken cancellationToken = default)
    {
        await _context.Transactions.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Transaction entity, CancellationToken cancellationToken = default)
    {
        _context.Transactions.Update(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var transaction = await _context.Transactions.FindAsync([id], cancellationToken);
        if (transaction is null) return;

        transaction.SoftDelete();
        await _context.SaveChangesAsync(cancellationToken);
    }
}
