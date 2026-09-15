using DecisionVault.Application.DTOs;
using DecisionVault.Application.Interfaces;
using DecisionVault.Domain;
using DecisionVault.Domain.Entities;
using DecisionVault.Domain.Enums;
using DecisionVault.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace DecisionVault.Application.Services;

/// <summary>
/// Dashboard and analytics aggregation. Grouped rows are fetched with translatable
/// SQL aggregates; label mapping (enum ToString) happens client-side. Formulas
/// delegate to <see cref="DecisionMetrics"/> so domain logic is not duplicated.
/// </summary>
public class DashboardService(IUnitOfWork uow) : IDashboardService
{
    public async Task<DashboardDto> GetUserDashboardAsync(int userId, CancellationToken ct = default)
    {
        var decisions = uow.Decisions.QueryWhere(d => d.UserId == userId);

        var total = await decisions.CountAsync(ct);
        var reviewed = decisions.Where(d => d.Review != null);
        var successful = await reviewed.CountAsync(d => d.Review!.IsSuccessful, ct);
        var unsuccessful = await reviewed.CountAsync(d => !d.Review!.IsSuccessful, ct);
        var pending = total - successful - unsuccessful;

        var avgConfidence = await decisions
            .Where(d => d.ConfidenceScore != null)
            .AverageAsync(d => (double?)d.ConfidenceScore, ct) ?? 0;

        // Decision Accuracy: mean alignment between expected success and actual outcome.
        var alignmentRows = await reviewed
            .Where(d => d.ExpectedSuccessScore != null)
            .Select(d => new { Expected = d.ExpectedSuccessScore!.Value, Actual = d.Review!.OutcomeRating })
            .ToListAsync(ct);
        var accuracy = alignmentRows.Count == 0 ? 0 :
            Math.Round(alignmentRows.Average(r => DecisionMetrics.AlignmentScore(r.Expected, r.Actual)), 1);

        var byCategoryRows = await decisions
            .GroupBy(d => new { d.CategoryId, Name = d.Category.Name })
            .Select(g => new { g.Key.CategoryId, g.Key.Name, Count = g.Count() })
            .ToListAsync(ct);
        var byCategory = byCategoryRows
            .Select(r => new CategoryCountDto(r.CategoryId, r.Name, r.Count))
            .OrderByDescending(c => c.Count)
            .ToList();

        var byStatusRows = await decisions
            .GroupBy(d => d.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var byStatus = byStatusRows
            .Select(r => new StatusCountDto(r.Status.ToString(), r.Count))
            .ToList();

        var outcomeDistribution = new List<OutcomeLabelCountDto>
        {
            new("Successful", successful),
            new("Unsuccessful", unsuccessful),
            new("Pending Review", pending)
        };

        var recent = await decisions
            .OrderByDescending(d => d.CreatedAt)
            .Take(5)
            .Select(d => new DecisionListItemDto(
                d.Id, d.Title, d.Status.ToString(), d.Category.Name,
                d.ConfidenceScore, d.ExpectedSuccessScore,
                d.Review != null ? d.Review.IsSuccessful : (bool?)null,
                d.Review != null ? d.Review.OutcomeRating : (int?)null,
                d.Options.Count, d.CreatedAt, d.ReviewDate))
            .ToListAsync(ct);

        var monthStart = new DateTime(DateTime.UtcNow.AddMonths(-11).Year, DateTime.UtcNow.AddMonths(-11).Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var trendRows = await decisions
            .Where(d => d.CreatedAt >= monthStart)
            .GroupBy(d => new { d.CreatedAt.Year, d.CreatedAt.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Total = g.Count(),
                Successful = g.Count(d => d.Review != null && d.Review.IsSuccessful)
            })
            .ToListAsync(ct);
        var trend = trendRows
            .Select(r => new MonthlyTrendPointDto(r.Year, r.Month, r.Total, r.Successful))
            .OrderBy(p => p.Year).ThenBy(p => p.Month)
            .ToList();

        return new DashboardDto(
            total, pending, successful, unsuccessful, Math.Round(avgConfidence, 1), accuracy,
            byCategory, outcomeDistribution, byStatus, recent, trend);
    }

    public async Task<AnalyticsDto> GetUserAnalyticsAsync(int userId, CancellationToken ct = default)
    {
        var decisions = uow.Decisions.QueryWhere(d => d.UserId == userId);

        var confidences = await decisions
            .Where(d => d.ConfidenceScore != null)
            .Select(d => d.ConfidenceScore!.Value)
            .ToListAsync(ct);

        var confidenceDistribution = confidences
            .GroupBy(Bucket)
            .Select(g => new ConfidenceBucketDto(g.Key, g.Count()))
            .OrderBy(b => b.Bucket)
            .ToList();

        var reviewedRows = await decisions
            .Where(d => d.Review != null && d.ExpectedSuccessScore != null)
            .OrderByDescending(d => d.Review!.ReviewedAt)
            .Take(8)
            .Select(d => new
            {
                d.Id,
                d.Title,
                Expected = d.ExpectedSuccessScore!.Value,
                Actual = d.Review!.OutcomeRating
            })
            .ToListAsync(ct);
        var expectedVsActual = reviewedRows
            .Select(r => new ExpectedVsActualDto(r.Id, r.Title, r.Expected,
                (int)Math.Round(r.Actual / 5d * 100d)))
            .ToList();

        var reviewMetrics = await decisions
            .Where(d => d.Review != null && d.ExpectedSuccessScore != null)
            .Select(d => new
            {
                Expected = d.ExpectedSuccessScore!.Value,
                d.Review!.OutcomeRating,
                d.Review.IsSuccessful
            })
            .ToListAsync(ct);
        var performanceScore = DecisionMetrics.DecisionPerformanceScore(
            reviewMetrics.Select(r => (r.OutcomeRating, r.Expected, r.IsSuccessful)).ToList());

        var monthStart = new DateTime(DateTime.UtcNow.AddMonths(-11).Year, DateTime.UtcNow.AddMonths(-11).Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var trendRows = await decisions
            .Where(d => d.CreatedAt >= monthStart && d.Review != null)
            .GroupBy(d => new { d.CreatedAt.Year, d.CreatedAt.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Total = g.Count(),
                Successful = g.Count(d => d.Review!.IsSuccessful)
            })
            .ToListAsync(ct);
        var successRateTrend = trendRows
            .Select(r => new SuccessRatePointDto(r.Year, r.Month, Math.Round(r.Successful * 100d / r.Total, 1)))
            .OrderBy(p => p.Year).ThenBy(p => p.Month)
            .ToList();

        return new AnalyticsDto(confidenceDistribution, expectedVsActual, performanceScore, successRateTrend);
    }

    public async Task<ProfileSummaryDto> GetProfileSummaryAsync(int userId, CancellationToken ct = default)
    {
        var decisions = uow.Decisions.QueryWhere(d => d.UserId == userId);
        var reviewed = decisions.Where(d => d.Review != null);

        var memberSince = await uow.Users.QueryWhere(u => u.Id == userId)
            .Select(u => u.CreatedAt).FirstOrDefaultAsync(ct);

        var total = await decisions.CountAsync(ct);
        var reviewedCount = await reviewed.CountAsync(ct);
        var avgRating = await reviewed.AverageAsync(d => (double?)d.Review!.OutcomeRating, ct) ?? 0;

        return new ProfileSummaryDto(memberSince, total, reviewedCount, Math.Round(avgRating, 2));
    }

    private static string Bucket(int v) =>
        v <= 20 ? "0-20" : v <= 40 ? "21-40" : v <= 60 ? "41-60" : v <= 80 ? "61-80" : "81-100";
}
