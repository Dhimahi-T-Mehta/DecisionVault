using DecisionVault.Domain.Enums;

namespace DecisionVault.Domain.Entities;

public abstract class BaseEntity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class User : BaseEntity
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "User";
    public bool IsActive { get; set; } = true;

    public ICollection<Decision> Decisions { get; set; } = new List<Decision>();
}

public class Category : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public CategoryKind Kind { get; set; }
    public string? Description { get; set; }

    public ICollection<Decision> Decisions { get; set; } = new List<Decision>();
}

public class Decision : BaseEntity
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DecisionStatus Status { get; set; } = DecisionStatus.Draft;

    public DateTime? DecisionDate { get; set; }
    public DateTime? ReviewDate { get; set; }

    /// <summary>1-100. How confident the user was when finalizing.</summary>
    public int? ConfidenceScore { get; set; }
    /// <summary>1-100. Probability of success the user expected when finalizing.</summary>
    public int? ExpectedSuccessScore { get; set; }
    public string? ExpectedOutcome { get; set; }

    public int? SelectedOptionId { get; set; }
    public DecisionOption? SelectedOption { get; set; }

    public DecisionReview? Review { get; set; }

    public ICollection<DecisionOption> Options { get; set; } = new List<DecisionOption>();
    public ICollection<DecisionReason> Reasons { get; set; } = new List<DecisionReason>();
    public ICollection<DecisionEvent> Events { get; set; } = new List<DecisionEvent>();

    /// <summary>Derived success flag for reviewed decisions; null until reviewed.</summary>
    public bool? IsSuccessful => Review?.IsSuccessful;

    /// <summary>
    /// Forward-only lifecycle. Allowed moves: Draft→Evaluating, Evaluating→Decided,
    /// Decided→InProgress, InProgress→ReadyForReview. Re-open: Decided→Evaluating
    /// (selection cleared by the service so the choice must be made again); back-step
    /// Evaluating→Draft for abandoned evaluations. Reviewed is terminal.
    /// </summary>
    public static readonly IReadOnlyDictionary<DecisionStatus, DecisionStatus[]> AllowedTransitions =
        new Dictionary<DecisionStatus, DecisionStatus[]>
        {
            [DecisionStatus.Draft] = [DecisionStatus.Evaluating],
            [DecisionStatus.Evaluating] = [DecisionStatus.Decided, DecisionStatus.Draft],
            [DecisionStatus.Decided] = [DecisionStatus.InProgress, DecisionStatus.Evaluating],
            [DecisionStatus.InProgress] = [DecisionStatus.ReadyForReview],
            [DecisionStatus.ReadyForReview] = [DecisionStatus.Reviewed],
            [DecisionStatus.Reviewed] = []
        };

    public bool CanTransitionTo(DecisionStatus target) =>
        AllowedTransitions.TryGetValue(Status, out var targets) && targets.Contains(target);
}

public class DecisionOption : BaseEntity
{
    public int DecisionId { get; set; }
    public Decision Decision { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Advantages { get; set; }
    public string? Disadvantages { get; set; }
    /// <summary>0-10 subjective attractiveness of this option.</summary>
    public int? Score { get; set; }
    /// <summary>0-10 relative importance; default 5. Documented, deliberately simple.</summary>
    public int Weight { get; set; } = 5;
}

public class DecisionReason : BaseEntity
{
    public int DecisionId { get; set; }
    public Decision Decision { get; set; } = null!;

    public ReasonType Type { get; set; }
    /// <summary>Free-form short label: Financial, Career, Risk, ... (max 60 chars).</summary>
    public string Category { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}

public class DecisionReview : BaseEntity
{
    public int DecisionId { get; set; }
    public Decision Decision { get; set; } = null!;

    public string ActualOutcome { get; set; } = string.Empty;
    /// <summary>1-5.</summary>
    public int OutcomeRating { get; set; }
    public string? WhatWentWell { get; set; }
    public string? WhatWentWrong { get; set; }
    public string? LessonsLearned { get; set; }
    public bool WouldChooseAgain { get; set; }

    /// <summary>
    /// Transparent server-computed rule, documented in README:
    /// a decision counts successful iff rating >= 3 AND would choose again.
    /// </summary>
    public bool IsSuccessful { get; set; }

    public DateTime ReviewedAt { get; set; }
}

public class DecisionEvent : BaseEntity
{
    public int DecisionId { get; set; }
    public Decision Decision { get; set; } = null!;

    /// <summary>Machine-readable kind, e.g. "Created", "OptionAdded", "Finalized", "Reviewed".</summary>
    public string EventType { get; set; } = string.Empty;
    /// <summary>Human-readable description shown on the timeline. Never contains decision content.</summary>
    public string Description { get; set; } = string.Empty;
}
