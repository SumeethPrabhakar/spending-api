namespace SpendingApi.Domain.Primitives;

public sealed record Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);

    public static Error NotFound(string message) => new("NOT_FOUND", message);
    public static Error Validation(string message) => new("VALIDATION", message);
    public static Error Conflict(string message) => new("CONFLICT", message);
    public static Error Unauthorised(string message) => new("UNAUTHORISED", message);
}
