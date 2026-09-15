using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using DecisionVault.Application.DTOs;
using DecisionVault.Application.Interfaces;
using DecisionVault.Application.Services;
using DecisionVault.Infrastructure.Data;
using DecisionVault.Domain.Entities;
using DecisionVault.Domain.Enums;
using DecisionVault.Domain.Exceptions;

namespace DecisionVault.Tests;

/// <summary>
/// Shared fixture: AuthService + DecisionService backed by an EF InMemory database
/// with a fake token service / password hasher, so service rules run against
/// realistic query behavior (Includes, expression trees) rather than mocked LINQ.
/// </summary>
public sealed class ServiceFixture : IDisposable
{
    public DecisionVaultDbContext Db { get; } = CreateDb();
    public FakeHasher Hasher { get; } = new();
    public FakeTokenService Tokens { get; } = new();

    public AuthService AuthService { get; }
    public DecisionService DecisionService { get; }
    public AdminService AdminService { get; }

    public Category Career { get; }
    public User DemoUser { get; }
    public User AdminUser { get; }

    public ServiceFixture()
    {
        AuthService = new AuthService(NewUoW(), Hasher, Tokens, NullLogger<AuthService>.Instance);
        DecisionService = new DecisionService(NewUoW(), NullLogger<DecisionService>.Instance);
        AdminService = new AdminService(NewUoW());

        Career = new Category { Name = "Career", Kind = CategoryKind.Career, CreatedAt = DateTime.UtcNow };
        DemoUser = new User { FullName = "Demo User", Email = "demo@test.local", PasswordHash = "h:Demo#12345", Role = "User", IsActive = true, CreatedAt = DateTime.UtcNow };
        AdminUser = new User { FullName = "Admin", Email = "admin@test.local", PasswordHash = "h:Admin#12345", Role = "Admin", IsActive = true, CreatedAt = DateTime.UtcNow };
        Db.Categories.Add(Career);
        Db.Users.AddRange(DemoUser, AdminUser);
        Db.SaveChanges();
        // Detach so services see fresh instances via their own contexts.
        Db.ChangeTracker.Clear();
    }

    public UnitOfWork NewUoW() => new(Db);

    public void Dispose() => Db.Dispose();

    public static DecisionVaultDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<DecisionVaultDbContext>()
            .UseInMemoryDatabase($"dv-tests-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new DecisionVaultDbContext(options);
    }
}

public sealed class FakeHasher : IPasswordHasher
{
    public string Hash(string password) => $"h:{password}";
    public bool Verify(string password, string hash) => hash == $"h:{password}";
}

public sealed class FakeTokenService : IJwtTokenService
{
    public (string Token, DateTime ExpiresAt) CreateToken(int userId, string email, string role) =>
        ($"token:{userId}:{role}", DateTime.UtcNow.AddHours(1));
}

/// <summary>Moq-based IRepository stub used to verify UnitOfWork/repository delegation.</summary>
public static class RepoMoq
{
    public static Mock<IRepository<T>> Of<T>() where T : BaseEntity => new();
}

public class AuthServiceTests : IClassFixture<ServiceFixture>
{
    private readonly ServiceFixture _fx;
    public AuthServiceTests(ServiceFixture fx) => _fx = fx;

    [Fact]
    public async Task Register_DuplicateEmail_ThrowsConflict()
    {
        var uow = _fx.NewUoW();
        var svc = new AuthService(uow, _fx.Hasher, _fx.Tokens, NullLogger<AuthService>.Instance);
        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => svc.RegisterAsync(new RegisterRequest("Another Demo", "demo@test.local", "Password#1")));
        Assert.Contains("already exists", ex.Message);
    }

    [Fact]
    public async Task Register_ValidCredentials_NormalizesEmail_AndHashesPassword()
    {
        var uow = _fx.NewUoW();
        var svc = new AuthService(uow, _fx.Hasher, _fx.Tokens, NullLogger<AuthService>.Instance);
        var res = await svc.RegisterAsync(new RegisterRequest("New Person", "MixedCase@Test.LOCAL", "Password#1"));

        Assert.Equal("mixedcase@test.local", res.User.Email);
        Assert.Equal("h:Password#1", _fx.Db.Users.Single(u => u.Id == res.User.Id).PasswordHash);
        Assert.Equal("User", res.User.Role);
        Assert.StartsWith("token:", res.Token);
    }

    [Fact]
    public async Task Register_WeakPassword_ThrowsValidation()
    {
        var uow = _fx.NewUoW();
        var svc = new AuthService(uow, _fx.Hasher, _fx.Tokens, NullLogger<AuthService>.Instance);
        await Assert.ThrowsAsync<DecisionVault.Domain.Exceptions.ValidationException>(
            () => svc.RegisterAsync(new RegisterRequest("Weak Pw", "weak@test.local", "short")));
    }

    [Fact]
    public async Task Login_BadPassword_ThrowsUnauthorized_WithUniformMessage()
    {
        var uow = _fx.NewUoW();
        var svc = new AuthService(uow, _fx.Hasher, _fx.Tokens, NullLogger<AuthService>.Instance);

        var wrongPw = await Assert.ThrowsAsync<UnauthorizedException>(
            () => svc.LoginAsync(new LoginRequest("demo@test.local", "nope")));
        var noUser = await Assert.ThrowsAsync<UnauthorizedException>(
            () => svc.LoginAsync(new LoginRequest("ghost@test.local", "nope")));

        // Uniform message: cannot distinguish existing email from wrong password.
        Assert.Equal(wrongPw.Message, noUser.Message);
    }

    [Fact]
    public async Task Login_InactiveAccount_ThrowsForbidden()
    {
        var uow = _fx.NewUoW();
        var svc = new AuthService(uow, _fx.Hasher, _fx.Tokens, NullLogger<AuthService>.Instance);
        await svc.RegisterAsync(new RegisterRequest("To Disable", "disable@test.local", "Password#1"));
        var user = _fx.Db.Users.Single(u => u.Email == "disable@test.local");
        user.IsActive = false;
        _fx.Db.SaveChanges();

        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => svc.LoginAsync(new LoginRequest("disable@test.local", "Password#1")));
        Assert.Contains("deactivated", ex.Message);
    }

    [Fact]
    public async Task ChangePassword_VerifiesCurrent_AndRotatesHash()
    {
        var uow = _fx.NewUoW();
        var svc = new AuthService(uow, _fx.Hasher, _fx.Tokens, NullLogger<AuthService>.Instance);
        var registered = await svc.RegisterAsync(new RegisterRequest("Pw Changer", "changer@test.local", "Old#12345"));

        await svc.ChangePasswordAsync(registered.User.Id, new ChangePasswordRequest("Old#12345", "New#12345"));
        Assert.Equal("h:New#12345", _fx.Db.Users.Single(u => u.Id == registered.User.Id).PasswordHash);

        // Wrong current password → ValidationException
        var uow2 = _fx.NewUoW();
        var svc2 = new AuthService(uow2, _fx.Hasher, _fx.Tokens, NullLogger<AuthService>.Instance);
        await Assert.ThrowsAsync<DecisionVault.Domain.Exceptions.ValidationException>(
            () => svc2.ChangePasswordAsync(registered.User.Id, new ChangePasswordRequest("wrong", "New#99999")));
    }

}
