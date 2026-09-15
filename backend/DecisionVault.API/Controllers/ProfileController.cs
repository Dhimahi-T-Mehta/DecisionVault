using DecisionVault.Application.DTOs;
using DecisionVault.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DecisionVault.API.Controllers;

/// <summary>Category catalog + admin management routes live in CategoriesController.</summary>
public static class ControllerExtensions
{
}

[ApiController]
[Route("api/profile")]
[Authorize]
public class ProfileController(IDashboardService dashboardService) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<ProfileSummaryDto>> GetSummary(CancellationToken ct) =>
        Ok(await dashboardService.GetProfileSummaryAsync(User.GetUserId(), ct));
}
