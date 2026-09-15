using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DecisionVault.Application.DTOs;
using DecisionVault.Application.Interfaces;

namespace DecisionVault.API.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController(IDashboardService dashboardService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DashboardDto>> GetDashboard(CancellationToken ct) =>
        Ok(await dashboardService.GetUserDashboardAsync(User.GetUserId(), ct));

    [HttpGet("analytics")]
    public async Task<ActionResult<AnalyticsDto>> GetAnalytics(CancellationToken ct) =>
        Ok(await dashboardService.GetUserAnalyticsAsync(User.GetUserId(), ct));

    [HttpGet("profile-summary")]
    public async Task<ActionResult<ProfileSummaryDto>> GetProfileSummary(CancellationToken ct) =>
        Ok(await dashboardService.GetProfileSummaryAsync(User.GetUserId(), ct));
}
