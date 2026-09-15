using DecisionVault.Application.Interfaces;

namespace DecisionVault.Infrastructure.Auth;

public class BcryptPasswordHasher : IPasswordHasher
{
    public string Hash(string password) =>
        BCrypt.Net.BCrypt.EnhancedHashPassword(password, workFactor: 11);

    public bool Verify(string password, string hash) =>
        BCrypt.Net.BCrypt.EnhancedVerify(password, hash);
}
