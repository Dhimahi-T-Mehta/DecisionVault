using DecisionVault.Application.DTOs;
using DecisionVault.Application.Interfaces;
using DecisionVault.Domain;
using DecisionVault.Domain.Entities;
using DecisionVault.Domain.Enums;
using DecisionVault.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace DecisionVault.Application.Services;

/// <summary>System-level admin queries. Deliberately returns metadata only — no decision content.</summary>
public class AdminService(IUnitOfWork uow) : IAdminService
{
    public async Task<AdminDashboardDto> GetDashboardAsync(CancellationToken ct = default)
    {
        var totalUsers = await uow.Users.Query().CountAsync(ct);
        var activeUsers = await uow.Users.Query().CountAsync(u => u.IsActive, ct);
        var totalDecisions = await uow.Decisions.Query().CountAsync(ct);

        var byStatusRows = await uow.Decisions.Query()
            .GroupBy(d => d.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var byStatus = byStatusRows
            .Select(r => new StatusCountDto(r.Status.ToString(), r.Count))
            .ToList();

        var byCategoryRows = await uow.Decisions.Query()
            .GroupBy(d => new { d.CategoryId, Name = d.Category.Name })
            .Select(g => new { g.Key.CategoryId, g.Key.Name, Count = g.Count() })
            .ToListAsync(ct);
        var byCategory = byCategoryRows
            .Select(r => new CategoryCountDto(r.CategoryId, r.Name, r.Count))
            .ToList();

        var reviewedCount = await uow.Decisions.Query().CountAsync(d => d.Review != null, ct);
        var successfulCount = await uow.Decisions.Query().CountAsync(d => d.Review != null && d.Review.IsSuccessful, ct);
        var successRate = reviewedCount == 0 ? 0 : Math.Round(successfulCount * 100d / reviewedCount, 1);

        var recentActivity = await uow.Events.Query()
            .OrderByDescending(ev => ev.CreatedAt).ThenByDescending(ev => ev.Id)
            .Take(15)
            .Select(ev => new ActivityDto(
                ev.Id, ev.EventType, ev.Description, ev.Decision.User.Email, ev.CreatedAt))
            .ToListAsync(ct);

        return new AdminDashboardDto(totalUsers, activeUsers, totalDecisions,
            byStatus, byCategory, successRate, recentActivity);
    }

    public async Task<PagedResult<AdminUserDto>> GetUsersAsync(string? search, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = uow.Users.Query();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(u => u.FullName.ToLower().Contains(term) || u.Email.ToLower().Contains(term));
        }

        var projected = query
            .OrderBy(u => u.CreatedAt)
            .Select(u => new AdminUserDto(
                u.Id, u.FullName, u.Email, u.Role, u.IsActive, u.CreatedAt, u.Decisions.Count));

        var totalCount = await projected.CountAsync(ct);
        var items = await projected
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return PagedResult<AdminUserDto>.Create(items, page, pageSize, totalCount);
    }

    public async Task<AdminUserDto> SetUserActiveAsync(int userId, bool isActive, int actingAdminId, CancellationToken ct = default)
    {
        if (userId == actingAdminId && !isActive)
            throw new ForbiddenException("You cannot deactivate your own account.");

        var user = await uow.Users.QueryWhere(u => u.Id == userId, asNoTracking: false).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("User not found.");

        user.IsActive = isActive;
        user.UpdatedAt = DateTime.UtcNow;
        await uow.SaveChangesAsync(ct);

        return new AdminUserDto(user.Id, user.FullName, user.Email, user.Role, user.IsActive,
            user.CreatedAt, user.Decisions.Count);
    }

    public async Task<PagedResult<ActivityDto>> GetActivityAsync(int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = uow.Events.Query().OrderByDescending(ev => ev.CreatedAt).ThenByDescending(ev => ev.Id);

        var projected = query.Select(ev => new ActivityDto(
            ev.Id, ev.EventType, ev.Description, ev.Decision.User.Email, ev.CreatedAt));

        var totalCount = await projected.CountAsync(ct);
        var items = await projected
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return PagedResult<ActivityDto>.Create(items, page, pageSize, totalCount);
    }
}
