using SpendingApi.Domain.Primitives;

namespace SpendingApi.Domain.Entities;

public sealed class Category
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string Icon { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    private Category(Guid id, string name, string icon)
    {
        Id = id;
        Name = name;
        Icon = icon;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public static Result<Category> Create(string name, string icon)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Error.Validation("Category name is required.");

        if (string.IsNullOrWhiteSpace(icon))
            return Error.Validation("Category icon is required.");

        return new Category(Guid.NewGuid(), name.Trim(), icon.Trim());
    }

    public void Delete() => DeletedAt = DateTime.UtcNow;

    public bool IsDeleted => DeletedAt.HasValue;
}
