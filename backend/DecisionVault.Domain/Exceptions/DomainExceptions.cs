namespace DecisionVault.Domain.Exceptions;

/// <summary>Base for expected domain failures mapped to 4xx responses by the exception middleware.</summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
}

/// <summary>Maps to 404.</summary>
public sealed class NotFoundException(string message) : DomainException(message);

/// <summary>Maps to 409 — invalid lifecycle transition, duplicates, in-use references.</summary>
public sealed class ConflictException(string message) : DomainException(message);

/// <summary>Maps to 400 — business-rule violations beyond input validation.</summary>
public sealed class ValidationException(string message, IEnumerable<string>? errors = null)
    : DomainException(message)
{
    public IReadOnlyList<string> Errors { get; } = errors?.ToList() ?? [];
}

/// <summary>Maps to 403 — ownership or role violations after authentication.</summary>
public sealed class ForbiddenException(string message) : DomainException(message);

/// <summary>Maps to HTTP 401 — e.g. failed credential check.</summary>
public sealed class UnauthorizedException(string message) : DomainException(message);
