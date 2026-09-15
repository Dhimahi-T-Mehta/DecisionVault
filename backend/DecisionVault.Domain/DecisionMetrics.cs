namespace DecisionVault.Domain;

/// <summary>
/// Decision Intelligence formulas. All metrics are deterministic, explainable arithmetic —
/// no "AI". Documented in README (Decision Intelligence section) and surfaced in the UI.
/// </summary>
public static class DecisionMetrics
{
    /// <summary>
    /// Alignment of expectation vs reality for one reviewed decision (0-100):
    ///   actualScore = outcomeRating / 5 * 100
    ///   alignment   = 100 − |expectedSuccessScore − actualScore|   (floored at 0)
    /// A perfect prediction scores 100; being off by 40 points scores 60.
    /// </summary>
    public static double AlignmentScore(int expectedSuccessScore, int outcomeRating)
    {
        var actualScore = outcomeRating / 5d * 100d;
        return Math.Max(0, 100 - Math.Abs(expectedSuccessScore - actualScore));
    }

    /// <summary>
    /// Decision Performance Score (0-100), application-defined — NOT a scientific measure:
    ///   0.4 · mean(outcomeRating/5·100)
    /// + 0.3 · successRate%              (IsSuccessful rule: rating ≥ 3 AND wouldChooseAgain)
    /// + 0.3 · mean(alignment)
    /// Returns 0 when no reviewed decisions exist.
    /// </summary>
    public static double DecisionPerformanceScore(
        IReadOnlyCollection<(int OutcomeRating, int ExpectedSuccessScore, bool IsSuccessful)> reviews)
    {
        if (reviews.Count == 0) return 0;

        var avgOutcome = reviews.Average(r => r.OutcomeRating / 5d * 100d);
        var successRate = reviews.Count(r => r.IsSuccessful) * 100d / reviews.Count;
        var avgAlignment = reviews.Average(r => AlignmentScore(r.ExpectedSuccessScore, r.OutcomeRating));

        return Math.Round(0.4 * avgOutcome + 0.3 * successRate + 0.3 * avgAlignment, 1);
    }
}
