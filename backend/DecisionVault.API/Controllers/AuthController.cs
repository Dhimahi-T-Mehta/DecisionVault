using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DecisionVault.Application.DTOs;
using DecisionVault.Application.Interfaces;
using DecisionVault.API.Middleware;

namespace DecisionVault.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken ct) =>
        CreatedAtAction(nameof(Me), await authService.RegisterAsync(request, ct));

    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct) =>
        Ok(await authService.LoginAsync(request, ct));

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserDto>> Me(CancellationToken ct) =>
        Ok(await authService.GetByIdAsync(User.GetUserId(), ct));

    [HttpPut("profile")]
    [Authorize]
    public async Task<ActionResult<UserDto>> UpdateProfile(UpdateProfileRequest request, CancellationToken ct) =>
        Ok(await authService.UpdateProfileAsync(User.GetUserId(), request, ct));

    [HttpPut("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        await authService.ChangePasswordAsync(User.GetUserId(), request, ct);
        return Ok(new { message = "Password updated." });
    }
}
