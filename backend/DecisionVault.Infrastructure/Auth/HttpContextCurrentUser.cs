using DecisionVault.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace DecisionVault.Infrastructure.Auth;

/// <summary>Resolves the authenticated user's id/role from the current HttpContext.</summary>
public class HttpContextCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public int UserId => Principal?.GetUserId() ?? 0;

    public bool IsAdmin => Principal?.IsInRole("Admin") ?? false;
}
