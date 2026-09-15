using DecisionVault.Application.DTOs;
using DecisionVault.Application.Interfaces;
using DecisionVault.Application.Validation;
using DecisionVault.Domain.Entities;
using DecisionVault.Domain.Enums;
using DecisionVault.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DecisionVault.Application.Services;

/// <summary>
/// Core decision lifecycle service. All ownership and state-machine rules live here;
/// controllers stay thin.
/// </summary>
public class DecisionService(
    IUnitOfWork uow,
    ILogger<DecisionService> logger) : IDecisionService
{
    private const int MaxPageSize = 100;

    public async Task<PagedResult<DecisionListItemDto>> GetPagedForUserAsync(int userId, DecisionQuery q, CancellationToken ct = default)
    {
        var page = Math.Max(1, q.Page);
        var pageSize = Math.Clamp(q.PageSize, 1, MaxPageSize);

        var query = uow.Decisions.Query().Where(d => d.UserId == userId);

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = q.Search.Trim().ToLower();
            query = query.Where(d =>
                d.Title.ToLower().Contains(term) ||
                (d.Description != null && d.Description.ToLower().Contains(term)));
        }

        if (q.CategoryId is int categoryId)
            query = query.Where(d => d.CategoryId == categoryId);

        if (q.Status is string status && Enum.TryParse<DecisionStatus>(status, ignoreCase: true, out var st))
            query = query.Where(d => d.Status == st);

        if (q.Outcome is string outcome)
        {
            query = outcome.ToLowerInvariant() switch
            {
                "successful" => query.Where(d => d.Review != null && d.Review.IsSuccessful),
                "unsuccessful" => query.Where(d => d.Review != null && !d.Review.IsSuccessful),
                "pendingreview" => query.Where(d =>
                    d.Status != DecisionStatus.Reviewed ||
                    (d.Status == DecisionStatus.Reviewed && d.Review == null)),
                _ => query
            };
        }

        if (q.MinConfidence is int min) query = query.Where(d => d.ConfidenceScore >= min);
        if (q.MaxConfidence is int max) query = query.Where(d => d.ConfidenceScore <= max);

        var descending = !string.Equals(q.SortDir, "asc", StringComparison.OrdinalIgnoreCase);
        query = (q.SortBy?.ToLowerInvariant()) switch
        {
            "title" => descending ? query.OrderByDescending(d => d.Title) : query.OrderBy(d => d.Title),
            "confidence" => descending ? query.OrderByDescending(d => d.ConfidenceScore) : query.OrderBy(d => d.ConfidenceScore),
            "reviewdate" => descending ? query.OrderByDescending(d => d.ReviewDate) : query.OrderBy(d => d.ReviewDate),
            _ => descending ? query.OrderByDescending(d => d.CreatedAt) : query.OrderBy(d => d.CreatedAt)
        };

        // Projection avoids materializing full entities (incl. description/expected text).
        var projected = query.Select(d => new DecisionListItemDto(
            d.Id,
            d.Title,
            d.Status.ToString(),
            d.Category.Name,
            d.ConfidenceScore,
            d.ExpectedSuccessScore,
            d.Review != null ? d.Review.IsSuccessful : (bool?)null,
            d.Review != null ? d.Review.OutcomeRating : (int?)null,
            d.Options.Count,
            d.CreatedAt,
            d.ReviewDate));

        var totalCount = await projected.CountAsync(ct);
        var items = await projected
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return PagedResult<DecisionListItemDto>.Create(items, page, pageSize, totalCount);
    }

    public async Task<DecisionDto> GetByIdForUserAsync(int id, int userId, bool isAdmin, CancellationToken ct = default)
    {
        var decision = await LoadOwnedDecisionAsync(id, userId, isAdmin, tracked: false, ct);

        return new DecisionDto(
            decision.Id, decision.Title, decision.Description, decision.Status.ToString(),
            decision.Category.Name, decision.CategoryId,
            decision.DecisionDate, decision.ReviewDate,
            decision.ConfidenceScore, decision.ExpectedSuccessScore, decision.ExpectedOutcome,
            decision.SelectedOptionId, decision.SelectedOption?.Name,
            decision.Options
                .OrderBy(o => o.Id)
                .Select(o => new DecisionOptionDto(o.Id, o.DecisionId, o.Name, o.Description,
                    o.Advantages, o.Disadvantages, o.Score, o.Weight))
                .ToList(),
            decision.Reasons
                .OrderBy(r => r.Id)
                .Select(r => new DecisionReasonDto(r.Id, r.DecisionId, r.Type.ToString(), r.Category, r.Text))
                .ToList(),
            decision.Review is null ? null : MapReview(decision.Review),
            decision.CreatedAt, decision.UpdatedAt ?? decision.CreatedAt);
    }

    public async Task<DecisionDto> CreateAsync(int userId, DecisionCreateRequest request, CancellationToken ct = default)
    {
        DtoValidator.ValidateDecisionRequest(request.Title, request.CategoryId, request.ConfidenceScore,
            request.ExpectedSuccessScore, request.ExpectedOutcome, request.DecisionDate, request.ReviewDate);

        await EnsureCategoryExistsAsync(request.CategoryId, ct);

        var decision = new Decision
        {
            UserId = userId,
            CategoryId = request.CategoryId,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            Status = DecisionStatus.Draft,
            DecisionDate = request.DecisionDate,
            ReviewDate = request.ReviewDate,
            ConfidenceScore = request.ConfidenceScore,
            ExpectedSuccessScore = request.ExpectedSuccessScore,
            ExpectedOutcome = request.ExpectedOutcome?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        await uow.Decisions.AddAsync(decision, ct);
        AddEvent(decision, EventTypes.Created, "Decision created");
        await uow.SaveChangesAsync(ct);

        logger.LogInformation("Decision created: DecisionId={DecisionId} UserId={UserId}", decision.Id, userId);
        return await GetByIdForUserAsync(decision.Id, userId, isAdmin: false, ct);
    }

    public async Task<DecisionDto> UpdateAsync(int id, int userId, DecisionUpdateRequest request, CancellationToken ct = default)
    {
        DtoValidator.ValidateDecisionRequest(request.Title, request.CategoryId, request.ConfidenceScore,
            request.ExpectedSuccessScore, request.ExpectedOutcome, request.DecisionDate, request.ReviewDate);

        var decision = await LoadOwnedDecisionAsync(id, userId, isAdmin: false, tracked: true, ct);
        await EnsureCategoryExistsAsync(request.CategoryId, ct);

        if (decision.Status == DecisionStatus.Reviewed &&
            (decision.ConfidenceScore != request.ConfidenceScore ||
             decision.ExpectedSuccessScore != request.ExpectedSuccessScore))
            throw new ConflictException("Expectations cannot change after review. Create a new decision instead.");

        decision.Title = request.Title.Trim();
        decision.Description = request.Description?.Trim();
        decision.CategoryId = request.CategoryId;
        decision.DecisionDate = request.DecisionDate;
        decision.ReviewDate = request.ReviewDate;
        decision.ConfidenceScore = request.ConfidenceScore;
        decision.ExpectedSuccessScore = request.ExpectedSuccessScore;
        decision.UpdatedAt = DateTime.UtcNow;

        await uow.SaveChangesAsync(ct);
        return await GetByIdForUserAsync(id, userId, isAdmin: false, ct);
    }

    public async Task DeleteAsync(int id, int userId, CancellationToken ct = default)
    {
        var decision = await LoadOwnedDecisionAsync(id, userId, isAdmin: false, tracked: true, ct);

        // Break the decision↔selected-option FK cycle explicitly before the cascade delete.
        if (decision.SelectedOptionId is not null)
        {
            decision.SelectedOptionId = null;
            await uow.SaveChangesAsync(ct);
        }

        uow.Decisions.Remove(decision);
        await uow.SaveChangesAsync(ct);
        logger.LogInformation("Decision deleted: DecisionId={DecisionId} UserId={UserId}", id, userId);
    }

    public async Task<DecisionOptionDto> AddOptionAsync(int decisionId, int userId, DecisionOptionRequest request, CancellationToken ct = default)
    {
        DtoValidator.ValidateOptionRequest(request.Name, request.Score, request.Weight);
        var decision = await LoadOwnedDecisionAsync(decisionId, userId, isAdmin: false, tracked: true, ct);

        if (decision.Status is not (DecisionStatus.Draft or DecisionStatus.Evaluating))
            throw new ConflictException("Options can only be modified while the decision is Draft or Evaluating.");

        var option = new DecisionOption
        {
            DecisionId = decisionId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Advantages = request.Advantages?.Trim(),
            Disadvantages = request.Disadvantages?.Trim(),
            Score = request.Score,
            Weight = request.Weight,
            CreatedAt = DateTime.UtcNow
        };
        await uow.Options.AddAsync(option, ct);
        AddEvent(decision, EventTypes.OptionAdded, $"Option '{option.Name}' added");
        decision.UpdatedAt = DateTime.UtcNow;
        await uow.SaveChangesAsync(ct);

        return new DecisionOptionDto(option.Id, option.DecisionId, option.Name, option.Description,
            option.Advantages, option.Disadvantages, option.Score, option.Weight);
    }

    public async Task<DecisionOptionDto> UpdateOptionAsync(int decisionId, int optionId, int userId, DecisionOptionRequest request, CancellationToken ct = default)
    {
        DtoValidator.ValidateOptionRequest(request.Name, request.Score, request.Weight);
        var decision = await LoadOwnedDecisionAsync(decisionId, userId, isAdmin: false, tracked: true, ct);

        if (decision.Status is not (DecisionStatus.Draft or DecisionStatus.Evaluating))
            throw new ConflictException("Options can only be modified while the decision is Draft or Evaluating.");

        var option = decision.Options.FirstOrDefault(o => o.Id == optionId)
            ?? throw new NotFoundException("Option not found for this decision.");

        option.Name = request.Name.Trim();
        option.Description = request.Description?.Trim();
        option.Advantages = request.Advantages?.Trim();
        option.Disadvantages = request.Disadvantages?.Trim();
        option.Score = request.Score;
        option.Weight = request.Weight;
        decision.UpdatedAt = DateTime.UtcNow;
        await uow.SaveChangesAsync(ct);

        return new DecisionOptionDto(option.Id, option.DecisionId, option.Name, option.Description,
            option.Advantages, option.Disadvantages, option.Score, option.Weight);
    }

    public async Task DeleteOptionAsync(int decisionId, int optionId, int userId, CancellationToken ct = default)
    {
        var decision = await LoadOwnedDecisionAsync(decisionId, userId, isAdmin: false, tracked: true, ct);
        var option = decision.Options.FirstOrDefault(o => o.Id == optionId)
            ?? throw new NotFoundException("Option not found for this decision.");

        if (decision.SelectedOptionId == optionId && decision.Status != DecisionStatus.Draft)
            throw new ConflictException("Cannot delete the selected option of a decided decision.");

        uow.Options.Remove(option);
        if (decision.SelectedOptionId == optionId) decision.SelectedOptionId = null;
        decision.UpdatedAt = DateTime.UtcNow;
        await uow.SaveChangesAsync(ct);
    }

    // ---------- Reasons ----------

    public async Task<DecisionReasonDto> AddReasonAsync(int decisionId, int userId, DecisionReasonRequest request, CancellationToken ct = default)
    {
        DtoValidator.ValidateReasonRequest(request.Type, request.Category, request.Text);
        var decision = await LoadOwnedDecisionAsync(decisionId, userId, isAdmin: false, tracked: true, ct);

        if (decision.Status is DecisionStatus.ReadyForReview or DecisionStatus.Reviewed)
            throw new ConflictException("Reasoning is closed after the decision is ready for review.");

        var reason = new DecisionReason
        {
            DecisionId = decisionId,
            Type = Enum.Parse<ReasonType>(request.Type, ignoreCase: true),
            Category = request.Category.Trim(),
            Text = request.Text.Trim(),
            CreatedAt = DateTime.UtcNow
        };
        await uow.Reasons.AddAsync(reason, ct);
        AddEvent(decision, EventTypes.ReasonAdded, $"{request.Type} reason recorded ({request.Category})");
        decision.UpdatedAt = DateTime.UtcNow;
        await uow.SaveChangesAsync(ct);

        return new DecisionReasonDto(reason.Id, reason.DecisionId, reason.Type.ToString(), reason.Category, reason.Text);
    }

    public async Task DeleteReasonAsync(int decisionId, int reasonId, int userId, CancellationToken ct = default)
    {
        var decision = await LoadOwnedDecisionAsync(decisionId, userId, isAdmin: false, tracked: true, ct);
        var reason = decision.Reasons.FirstOrDefault(r => r.Id == reasonId)
            ?? throw new NotFoundException("Reason not found for this decision.");
        uow.Reasons.Remove(reason);
        decision.UpdatedAt = DateTime.UtcNow;
        await uow.SaveChangesAsync(ct);
    }

    // ---------- Lifecycle ----------

    public async Task<DecisionDto> SelectOptionAsync(int decisionId, int userId, SelectOptionRequest request, CancellationToken ct = default)
    {
        var decision = await LoadOwnedDecisionAsync(decisionId, userId, isAdmin: false, tracked: true, ct);

        if (decision.Status is not (DecisionStatus.Draft or DecisionStatus.Evaluating))
            throw new ConflictException("An option can only be selected while Draft or Evaluating.");

        var option = decision.Options.FirstOrDefault(o => o.Id == request.OptionId)
            ?? throw new NotFoundException("Option not found for this decision.");

        decision.SelectedOptionId = option.Id;
        AddEvent(decision, EventTypes.OptionSelected, $"Option '{option.Name}' marked as intended choice");
        await TransitionInternalAsync(decision, DecisionStatus.Decided, "Option selected");
        await uow.SaveChangesAsync(ct);
        return await GetByIdForUserAsync(decisionId, userId, isAdmin: false, ct);
    }

    public async Task<DecisionDto> TransitionAsync(int decisionId, int userId, TransitionRequest request, CancellationToken ct = default)
    {
        var target = DtoValidator.ParseStatus(request.Status, "Allowed forward moves: Evaluating, Decided, InProgress, ReadyForReview.");
        var decision = await LoadOwnedDecisionAsync(decisionId, userId, isAdmin: false, tracked: true, ct);
        await TransitionInternalAsync(decision, target, $"Status changed to {target}");
        await uow.SaveChangesAsync(ct);
        return await GetByIdForUserAsync(decisionId, userId, isAdmin: false, ct);
    }

    public async Task<DecisionDto> FinalizeAsync(int decisionId, int userId, FinalizeRequest request, CancellationToken ct = default)
    {
        DtoValidator.ValidateDecisionRequest("Finalize", 1, request.ConfidenceScore, request.ExpectedSuccessScore,
            request.ExpectedOutcome, null, null);

        var decision = await LoadOwnedDecisionAsync(decisionId, userId, isAdmin: false, tracked: true, ct);

        if (decision.Status is DecisionStatus.ReadyForReview or DecisionStatus.Reviewed)
            throw new ConflictException("Decision is already finalized.");

        if (request.SelectedOptionId > 0)
        {
            var option = decision.Options.FirstOrDefault(o => o.Id == request.SelectedOptionId)
                ?? throw new NotFoundException("Selected option not found for this decision.");
            if (decision.SelectedOptionId != option.Id)
            {
                decision.SelectedOptionId = option.Id;
                AddEvent(decision, EventTypes.OptionSelected, $"Option '{option.Name}' selected");
            }
        }
        else if (decision.SelectedOptionId is null)
        {
            throw new ValidationException("Select an option before finalizing.");
        }

        if (decision.Status is DecisionStatus.Draft or DecisionStatus.Evaluating)
        {
            if (decision.Options.Count < 2)
                throw new ValidationException("At least two options are required to finalize a decision.");
            decision.Status = DecisionStatus.Decided;
        }

        decision.ConfidenceScore = request.ConfidenceScore;
        decision.ExpectedSuccessScore = request.ExpectedSuccessScore;
        decision.ExpectedOutcome = request.ExpectedOutcome.Trim();
        decision.DecisionDate ??= DateTime.UtcNow;
        decision.UpdatedAt = DateTime.UtcNow;

        AddEvent(decision, EventTypes.Finalized, "Decision finalized with confidence and expected outcome");
        await uow.SaveChangesAsync(ct);
        return await GetByIdForUserAsync(decisionId, userId, isAdmin: false, ct);
    }

    // ---------- Review ----------

    public async Task<DecisionReviewDto> SubmitReviewAsync(int decisionId, int userId, DecisionReviewRequest request, CancellationToken ct = default)
    {
        DtoValidator.ValidateReviewRequest(request.ActualOutcome, request.OutcomeRating);
        var decision = await LoadOwnedDecisionAsync(decisionId, userId, isAdmin: false, tracked: true, ct);

        if (decision.Status is not DecisionStatus.ReadyForReview)
            throw new ConflictException("Only decisions in 'ReadyForReview' can be reviewed. Move it through the lifecycle first.");

        if (decision.Review is not null)
            throw new ConflictException("This decision has already been reviewed.");

        if (decision.ConfidenceScore is null || decision.ExpectedSuccessScore is null)
            throw new ValidationException("Finalize the decision with confidence and expected success before reviewing.");

        var review = new DecisionReview
        {
            DecisionId = decisionId,
            ActualOutcome = request.ActualOutcome.Trim(),
            OutcomeRating = request.OutcomeRating,
            WhatWentWell = request.WhatWentWell?.Trim(),
            WhatWentWrong = request.WhatWentWrong?.Trim(),
            LessonsLearned = request.LessonsLearned?.Trim(),
            WouldChooseAgain = request.WouldChooseAgain,
            IsSuccessful = request.OutcomeRating >= 3 && request.WouldChooseAgain,
            ReviewedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        await uow.Reviews.AddAsync(review, ct);

        decision.Status = DecisionStatus.Reviewed;
        decision.ReviewDate ??= DateTime.UtcNow;
        decision.UpdatedAt = DateTime.UtcNow;
        AddEvent(decision, EventTypes.Reviewed, $"Review recorded — outcome rated {request.OutcomeRating}/5");

        await uow.SaveChangesAsync(ct);
        logger.LogInformation("Review submitted: DecisionId={DecisionId} UserId={UserId}", decisionId, userId);
        return MapReview(review);
    }

    public async Task<DecisionReviewDto> GetReviewAsync(int decisionId, int userId, bool isAdmin, CancellationToken ct = default)
    {
        var decision = await LoadOwnedDecisionAsync(decisionId, userId, isAdmin, tracked: false, ct);
        return decision.Review is null
            ? throw new NotFoundException("No review recorded for this decision yet.")
            : MapReview(decision.Review);
    }

    public async Task<IReadOnlyList<DecisionEventDto>> GetTimelineAsync(int decisionId, int userId, bool isAdmin, CancellationToken ct = default)
    {
        var decision = await LoadOwnedDecisionAsync(decisionId, userId, isAdmin, tracked: false, ct);
        return await uow.Events.QueryWhere(ev => ev.DecisionId == decision.Id)
            .OrderByDescending(ev => ev.CreatedAt).ThenByDescending(ev => ev.Id)
            .Select(ev => new DecisionEventDto(ev.Id, ev.DecisionId, ev.EventType, ev.Description, ev.CreatedAt))
            .ToListAsync(ct);
    }

    // ---------- helpers ----------

    private async Task<Decision> LoadOwnedDecisionAsync(int id, int userId, bool isAdmin, bool tracked, CancellationToken ct)
    {
        var query = uow.Decisions
            .QueryWhere(d => d.Id == id, asNoTracking: !tracked)
            .Include(d => d.Category)
            .Include(d => d.Review)
            .Include(d => d.Options)
            .Include(d => d.Reasons)
            .Include(d => d.SelectedOption);

        var decision = await query.FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Decision not found.");

        if (decision.UserId != userId && !isAdmin)
            throw new ForbiddenException("You do not have access to this decision.");

        return decision;
    }

    private async Task TransitionInternalAsync(Decision decision, DecisionStatus target, string eventDescription)
    {
        if (!decision.CanTransitionTo(target))
            throw new ConflictException($"Cannot move a {decision.Status} decision to {target}.");

        if (decision.Status == DecisionStatus.Evaluating && target == DecisionStatus.Decided && decision.SelectedOptionId is null)
            throw new ValidationException("Select an option before deciding.");

        if (decision.Status == DecisionStatus.Decided && target == DecisionStatus.Evaluating)
            decision.SelectedOptionId = null;

        decision.Status = target;
        decision.UpdatedAt = DateTime.UtcNow;
        AddEvent(decision, EventTypes.StatusChanged, eventDescription);
        await Task.CompletedTask;
    }

    private async Task EnsureCategoryExistsAsync(int categoryId, CancellationToken ct)
    {
        _ = await uow.Categories.QueryWhere(c => c.Id == categoryId).FirstOrDefaultAsync(ct)
            ?? throw new ValidationException("The selected category does not exist.");
    }

    private static void AddEvent(Decision decision, string eventType, string description) =>
        decision.Events.Add(new DecisionEvent
        {
            EventType = eventType,
            Description = description,
            CreatedAt = DateTime.UtcNow
        });

    private static DecisionReviewDto MapReview(DecisionReview r) =>
        new(r.Id, r.DecisionId, r.ActualOutcome, r.OutcomeRating, r.WhatWentWell,
            r.WhatWentWrong, r.LessonsLearned, r.WouldChooseAgain, r.IsSuccessful, r.ReviewedAt);
}

public static class EventTypes
{
    public const string Created = "Created";
    public const string OptionAdded = "OptionAdded";
    public const string ReasonAdded = "ReasonAdded";
    public const string OptionSelected = "OptionSelected";
    public const string StatusChanged = "StatusChanged";
    public const string Finalized = "Finalized";
    public const string Reviewed = "Reviewed";
}
