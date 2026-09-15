using DecisionVault.Domain;

namespace DecisionVault.Application.DTOs;

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int TotalPages)
{
    public static PagedResult<T> Create(IReadOnlyList<T> items, int page, int pageSize, int totalCount) =>
        new(items, page, pageSize, totalCount, (int)Math.Ceiling(totalCount / (double)pageSize));
}

// ---------- Auth ----------
public record RegisterRequest(string FullName, string Email, string Password);
public record LoginRequest(string Email, string Password);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public record UpdateProfileRequest(string FullName, string Email);

public record UserDto(int Id, string FullName, string Email, string Role, bool IsActive, DateTime CreatedAt);

public record AuthResponse(string Token, DateTime ExpiresAt, UserDto User);

// ---------- Categories ----------
public record CategoryDto(int Id, string Name, string Kind, string? Description);
public record CategoryUpsertRequest(string Name, string Kind, string? Description);

// ---------- Decision ----------
public record DecisionCreateRequest(
    string Title,
    string? Description,
    int CategoryId,
    DateTime? DecisionDate,
    DateTime? ReviewDate,
    int? ConfidenceScore,
    int? ExpectedSuccessScore,
    string? ExpectedOutcome);

public record DecisionUpdateRequest(
    string Title,
    string? Description,
    int CategoryId,
    DateTime? DecisionDate,
    DateTime? ReviewDate,
    int? ConfidenceScore,
    int? ExpectedSuccessScore,
    string? ExpectedOutcome);

public record DecisionOptionRequest(
    string Name,
    string? Description,
    string? Advantages,
    string? Disadvantages,
    int? Score,
    int Weight);

public record DecisionOptionDto(
    int Id,
    int DecisionId,
    string Name,
    string? Description,
    string? Advantages,
    string? Disadvantages,
    int? Score,
    int Weight);

public record DecisionReasonRequest(string Type, string Category, string Text);
public record DecisionReasonDto(int Id, int DecisionId, string Type, string Category, string Text);

public record SelectOptionRequest(int OptionId);
public record TransitionRequest(string Status);
public record FinalizeRequest(
    int SelectedOptionId,
    int ConfidenceScore,
    int ExpectedSuccessScore,
    string ExpectedOutcome);

public record DecisionReviewRequest(
    string ActualOutcome,
    int OutcomeRating,
    string? WhatWentWell,
    string? WhatWentWrong,
    string? LessonsLearned,
    bool WouldChooseAgain);

public record DecisionReviewDto(
    int Id,
    int DecisionId,
    string ActualOutcome,
    int OutcomeRating,
    string? WhatWentWell,
    string? WhatWentWrong,
    string? LessonsLearned,
    bool WouldChooseAgain,
    bool IsSuccessful,
    DateTime ReviewedAt);

public record DecisionEventDto(int Id, int DecisionId, string EventType, string Description, DateTime CreatedAt);

public record DecisionListItemDto(
    int Id,
    string Title,
    string Status,
    string CategoryName,
    int? ConfidenceScore,
    int? ExpectedSuccessScore,
    bool? IsSuccessful,
    int? OutcomeRating,
    int OptionsCount,
    DateTime CreatedAt,
    DateTime? ReviewDate);

public record DecisionDto(
    int Id,
    string Title,
    string? Description,
    string Status,
    string CategoryName,
    int CategoryId,
    DateTime? DecisionDate,
    DateTime? ReviewDate,
    int? ConfidenceScore,
    int? ExpectedSuccessScore,
    string? ExpectedOutcome,
    int? SelectedOptionId,
    string? SelectedOptionName,
    IReadOnlyList<DecisionOptionDto> Options,
    IReadOnlyList<DecisionReasonDto> Reasons,
    DecisionReviewDto? Review,
    DateTime CreatedAt,
    DateTime UpdatedAt);

// ---------- Dashboard / Analytics ----------
public record CategoryCountDto(int CategoryId, string CategoryName, int Count);
public record StatusCountDto(string Status, int Count);
public record OutcomeLabelCountDto(string Label, int Count);
public record MonthlyTrendPointDto(int Year, int Month, int Total, int Successful);
public record DashboardDto(
    int TotalDecisions,
    int PendingReview,
    int Successful,
    int Unsuccessful,
    double AverageConfidence,
    double DecisionAccuracy,
    IReadOnlyList<CategoryCountDto> DecisionsByCategory,
    IReadOnlyList<OutcomeLabelCountDto> OutcomeDistribution,
    IReadOnlyList<StatusCountDto> DecisionsByStatus,
    IReadOnlyList<DecisionListItemDto> RecentDecisions,
    IReadOnlyList<MonthlyTrendPointDto> MonthlyTrend);

public record ConfidenceBucketDto(string Bucket, int Count);
public record ExpectedVsActualDto(int DecisionId, string Title, int Expected, int Actual);
public record SuccessRatePointDto(int Year, int Month, double SuccessRate);
public record AnalyticsDto(
    IReadOnlyList<ConfidenceBucketDto> ConfidenceDistribution,
    IReadOnlyList<ExpectedVsActualDto> ExpectedVsActual,
    double DecisionPerformanceScore,
    IReadOnlyList<SuccessRatePointDto> SuccessRateTrend);

// ---------- Admin ----------
public record AdminUserDto(int Id, string FullName, string Email, string Role, bool IsActive, DateTime CreatedAt, int DecisionCount);
public record UpdateUserStatusRequest(bool IsActive);
public record ActivityDto(int Id, string EventType, string Description, string UserEmail, DateTime CreatedAt);
public record UpdateUserRoleRequest(string Role);
public record AdminDashboardDto(
    int TotalUsers,
    int ActiveUsers,
    int TotalDecisions,
    IReadOnlyList<StatusCountDto> DecisionsByStatus,
    IReadOnlyList<CategoryCountDto> DecisionsByCategory,
    double OverallSuccessRate,
    IReadOnlyList<ActivityDto> RecentActivity);

// ---------- Profile ----------
public record ProfileSummaryDto(DateTime MemberSince, int TotalDecisions, int ReviewedCount, double AvgOutcomeRating);
