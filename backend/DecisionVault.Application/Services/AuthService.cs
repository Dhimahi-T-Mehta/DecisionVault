using DecisionVault.Application.DTOs;
using DecisionVault.Application.Interfaces;
using DecisionVault.Application.Validation;
using DecisionVault.Domain.Entities;
using DecisionVault.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DecisionVault.Application.Services;

public class AuthService(
    IUnitOfWork uow,
    IPasswordHasher hasher,
    IJwtTokenService tokenService,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        DtoValidator.ValidateRegistration(request.FullName, request.Email, request.Password);
        var email = request.Email.Trim().ToLowerInvariant();

        var existing = await uow.Users.QueryWhere(u => u.Email == email).FirstOrDefaultAsync(ct);
        if (existing is not null)
            throw new ConflictException("An account with this email already exists.");

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PasswordHash = hasher.Hash(request.Password),
            Role = "User",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        await uow.Users.AddAsync(user, ct);
        await uow.SaveChangesAsync(ct);

        logger.LogInformation("New user registered: UserId={UserId}", user.Id);
        return CreateAuthResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await uow.Users.QueryWhere(u => u.Email == email, asNoTracking: false)
            .FirstOrDefaultAsync(ct);

        // Uniform failure message: never reveal whether the email exists.
        if (user is null || !hasher.Verify(request.Password, user.PasswordHash))
        {
            logger.LogInformation("Failed login attempt for email hash {EmailHash}", email.GetHashCode());
            throw new UnauthorizedException("Invalid email or password.");
        }

        if (!user.IsActive)
            throw new ForbiddenException("This account has been deactivated. Contact an administrator.");

        logger.LogInformation("User logged in: UserId={UserId}", user.Id);
        return CreateAuthResponse(user);
    }

    public async Task<UserDto> GetByIdAsync(int userId, CancellationToken ct = default)
    {
        var user = await uow.Users.QueryWhere(u => u.Id == userId).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("User not found.");
        return ToDto(user);
    }

    public async Task<UserDto> UpdateProfileAsync(int userId, UpdateProfileRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Trim().Length < 2 || request.FullName.Length > 100)
            throw new Domain.Exceptions.ValidationException("Full name must be 2-100 characters.");
        var email = request.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || email.Length > 255)
            throw new Domain.Exceptions.ValidationException("A valid email is required.");

        var user = await uow.Users.QueryWhere(u => u.Id == userId, asNoTracking: false).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("User not found.");

        var emailTaken = await uow.Users.QueryWhere(u => u.Email == email && u.Id != userId).AnyAsync(ct);
        if (emailTaken)
            throw new ConflictException("Another account already uses this email.");

        user.FullName = request.FullName.Trim();
        user.Email = email;
        user.UpdatedAt = DateTime.UtcNow;
        await uow.SaveChangesAsync(ct);

        logger.LogInformation("Profile updated: UserId={UserId}", userId);
        return ToDto(user);
    }

    public async Task ChangePasswordAsync(int userId, ChangePasswordRequest request, CancellationToken ct = default)
    {
        DtoValidator.ValidatePassword(request.NewPassword);

        var user = await uow.Users.QueryWhere(u => u.Id == userId, asNoTracking: false).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("User not found.");

        if (!hasher.Verify(request.CurrentPassword, user.PasswordHash))
            throw new Domain.Exceptions.ValidationException("Current password is incorrect.");

        user.PasswordHash = hasher.Hash(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await uow.SaveChangesAsync(ct);
        logger.LogInformation("Password changed: UserId={UserId}", userId);
    }

    private AuthResponse CreateAuthResponse(User user)
    {
        var (token, expiresAt) = tokenService.CreateToken(user.Id, user.Email, user.Role);
        return new AuthResponse(token, expiresAt, ToDto(user));
    }

    private static UserDto ToDto(User user) =>
        new(user.Id, user.FullName, user.Email, user.Role, user.IsActive, user.CreatedAt);
}

