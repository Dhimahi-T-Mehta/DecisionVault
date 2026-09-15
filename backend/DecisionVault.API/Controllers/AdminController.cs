using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DecisionVault.Application.DTOs;
using DecisionVault.Application.Interfaces;

namespace DecisionVault.API.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController(IAdminService adminService) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<ActionResult<AdminDashboardDto>> GetDashboard(CancellationToken ct) =>
        Ok(await adminService.GetDashboardAsync(ct));

    [HttpGet("users")]
    public async Task<ActionResult<PagedResult<AdminUserDto>>> GetUsers(
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        CancellationToken ct = default) =>
        Ok(await adminService.GetUsersAsync(search, page, pageSize, ct));

    [HttpPut("users/{id:int}/status")]
    public async Task<ActionResult<AdminUserDto>> SetUserStatus(int id, UpdateUserStatusRequest request, CancellationToken ct) =>
        Ok(await adminService.SetUserActiveAsync(id, request.IsActive, User.GetUserId(), ct));

    [HttpPut("users/{id:int}/role")]
    public async Task<ActionResult<AdminUserDto>> SetUserRole(int id, UpdateUserRoleRequest request, CancellationToken ct) =>
        Ok(await adminService.SetUserRoleAsync(id, request.Role, User.GetUserId(), ct));

    [HttpGet("activity")]
    public async Task<ActionResult<PagedResult<ActivityDto>>> GetActivity(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(await adminService.GetActivityAsync(page, pageSize, ct));
}
