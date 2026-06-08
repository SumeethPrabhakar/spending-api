using Microsoft.EntityFrameworkCore;
using SpendingApi.Application.Abstractions;
using SpendingApi.Domain.Entities;
using SpendingApi.Domain.Primitives;

namespace SpendingApi.Infrastructure.Persistence.Repositories;

public sealed class CategoryRepository : IReadOnlyRepository<Category>
{
    private readonly SpendingDbContext _context;

    public CategoryRepository(SpendingDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Category>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var category = await _context.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (category is null)
            return Error.NotFound($"Category {id} not found.");

        return category;
    }

    public async Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Categories
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public Task<IReadOnlyList<Category>> FindAsync(ISpecification<Category> specification, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Use GetAllAsync or GetByIdAsync for categories.");
}
