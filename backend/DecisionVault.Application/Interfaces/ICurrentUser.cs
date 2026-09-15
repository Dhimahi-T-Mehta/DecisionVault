using System.Security.Claims;

namespace DecisionVault.Application.Interfaces;

public interface IJwtTokenService
{
    /// <summary>Returns the signed JWT and its UTC expiry.</summary>
    (string Token, DateTime ExpiresAt) CreateToken(int userId, string email, string role);
}

/// <summary>Abstraction over BCrypt so Application does not reference the hashing library.</summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

/// <summary>Current authenticated principal accessor, registered scoped from HttpContext.</summary>
public interface ICurrentUser
{
    int UserId { get; }
    bool IsAdmin { get; }
}

public static class ClaimsPrincipalExtensions
{
    public static int GetUserId(this ClaimsPrincipal principal) =>
        int.TryParse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;
}
