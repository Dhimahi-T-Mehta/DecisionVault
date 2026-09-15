using DecisionVault.Application.DTOs;

namespace DecisionVault.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<UserDto> GetByIdAsync(int userId, CancellationToken ct = default);
    Task<UserDto> UpdateProfileAsync(int userId, UpdateProfileRequest request, CancellationToken ct = default);
    Task ChangePasswordAsync(int userId, ChangePasswordRequest request, CancellationToken ct = default);
}

public interface IDecisionService
{
    Task<PagedResult<DecisionListItemDto>> GetPagedForUserAsync(int userId, DecisionQuery query, CancellationToken ct = default);
    Task<DecisionDto> GetByIdForUserAsync(int id, int userId, bool isAdmin, CancellationToken ct = default);
    Task<DecisionDto> CreateAsync(int userId, DecisionCreateRequest request, CancellationToken ct = default);
    Task<DecisionDto> UpdateAsync(int id, int userId, DecisionUpdateRequest request, CancellationToken ct = default);
    Task DeleteAsync(int id, int userId, CancellationToken ct = default);

    Task<DecisionOptionDto> AddOptionAsync(int decisionId, int userId, DecisionOptionRequest request, CancellationToken ct = default);
    Task<DecisionOptionDto> UpdateOptionAsync(int decisionId, int optionId, int userId, DecisionOptionRequest request, CancellationToken ct = default);
    Task DeleteOptionAsync(int decisionId, int optionId, int userId, CancellationToken ct = default);

    Task<DecisionReasonDto> AddReasonAsync(int decisionId, int userId, DecisionReasonRequest request, CancellationToken ct = default);
    Task DeleteReasonAsync(int decisionId, int reasonId, int userId, CancellationToken ct = default);

    Task<DecisionDto> SelectOptionAsync(int decisionId, int userId, SelectOptionRequest request, CancellationToken ct = default);
    Task<DecisionDto> TransitionAsync(int decisionId, int userId, TransitionRequest request, CancellationToken ct = default);
    Task<DecisionDto> FinalizeAsync(int decisionId, int userId, FinalizeRequest request, CancellationToken ct = default);

    Task<DecisionReviewDto> SubmitReviewAsync(int decisionId, int userId, DecisionReviewRequest request, CancellationToken ct = default);
    Task<DecisionReviewDto> GetReviewAsync(int decisionId, int userId, bool isAdmin, CancellationToken ct = default);
    Task<IReadOnlyList<DecisionEventDto>> GetTimelineAsync(int decisionId, int userId, bool isAdmin, CancellationToken ct = default);
}

public record DecisionQuery(
    string? Search,
    int? CategoryId,
    string? Status,
    string? Outcome,
    int? MinConfidence,
    int? MaxConfidence,
    string SortBy,
    string SortDir,
    int Page,
    int PageSize);

public interface IDashboardService
{
    Task<DashboardDto> GetUserDashboardAsync(int userId, CancellationToken ct = default);
    Task<AnalyticsDto> GetUserAnalyticsAsync(int userId, CancellationToken ct = default);
    Task<ProfileSummaryDto> GetProfileSummaryAsync(int userId, CancellationToken ct = default);
}

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken ct = default);
    Task<CategoryDto> CreateAsync(CategoryUpsertRequest request, CancellationToken ct = default);
    Task<CategoryDto> UpdateAsync(int id, CategoryUpsertRequest request, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public interface IAdminService
{
    Task<AdminDashboardDto> GetDashboardAsync(CancellationToken ct = default);
    Task<PagedResult<AdminUserDto>> GetUsersAsync(string? search, int page, int pageSize, CancellationToken ct = default);
    Task<AdminUserDto> SetUserActiveAsync(int userId, bool isActive, int actingAdminId, CancellationToken ct = default);
    Task<AdminUserDto> SetUserRoleAsync(int userId, string role, int actingAdminId, CancellationToken ct = default);
    Task<PagedResult<ActivityDto>> GetActivityAsync(int page, int pageSize, CancellationToken ct = default);
}
