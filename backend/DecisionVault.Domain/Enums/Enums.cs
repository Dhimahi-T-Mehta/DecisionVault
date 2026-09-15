namespace DecisionVault.Domain.Enums;

/// <summary>Decision lifecycle state machine. Order is meaningful: forward-only transitions.</summary>
public enum DecisionStatus
{
    Draft = 0,
    Evaluating = 1,
    Decided = 2,
    InProgress = 3,
    ReadyForReview = 4,
    Reviewed = 5
}

/// <summary>Reasoning polarity recorded for a decision.</summary>
public enum ReasonType
{
    Pro = 0,
    Con = 1,
    Note = 2
}

/// <summary>Built-in decision category kinds used for badge styling and defaults.</summary>
public enum CategoryKind
{
    Career = 0,
    Education = 1,
    Financial = 2,
    Health = 3,
    Technology = 4,
    Personal = 5,
    Business = 6,
    Other = 7
}
