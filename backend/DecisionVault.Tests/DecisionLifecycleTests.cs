using DecisionVault.Domain;
using DecisionVault.Domain.Entities;
using DecisionVault.Domain.Enums;
using DecisionVault.Domain.Exceptions;

namespace DecisionVault.Tests;

/// <summary>Forward-only lifecycle rules on the Decision entity.</summary>
public class DecisionLifecycleTests
{
    [Theory]
    [InlineData(DecisionStatus.Draft, DecisionStatus.Evaluating, true)]
    [InlineData(DecisionStatus.Evaluating, DecisionStatus.Decided, true)]
    [InlineData(DecisionStatus.Evaluating, DecisionStatus.Draft, true)]
    [InlineData(DecisionStatus.Decided, DecisionStatus.InProgress, true)]
    [InlineData(DecisionStatus.InProgress, DecisionStatus.ReadyForReview, true)]
    [InlineData(DecisionStatus.ReadyForReview, DecisionStatus.Reviewed, true)]
    [InlineData(DecisionStatus.Draft, DecisionStatus.Decided, false)]
    [InlineData(DecisionStatus.Draft, DecisionStatus.Reviewed, false)]
    [InlineData(DecisionStatus.Decided, DecisionStatus.Evaluating, false)]
    [InlineData(DecisionStatus.Decided, DecisionStatus.Reviewed, false)]
    [InlineData(DecisionStatus.Reviewed, DecisionStatus.Draft, false)]
    [InlineData(DecisionStatus.Reviewed, DecisionStatus.Reviewed, false)]
    public void CanTransitionTo_EnforcesForwardOnlyLifecycle(DecisionStatus from, DecisionStatus to, bool allowed)
    {
        var decision = new Decision { Status = from };
        Assert.Equal(allowed, decision.CanTransitionTo(to));
    }

    [Fact]
    public void Reviewed_IsTerminal_AllTransitionsRejected()
    {
        var decision = new Decision { Status = DecisionStatus.Reviewed };
        foreach (DecisionStatus target in Enum.GetValues<DecisionStatus>())
        {
            if (target == DecisionStatus.Reviewed) continue;
            Assert.False(decision.CanTransitionTo(target));
        }
    }
}

/// <summary>Pure scoring formulas in DecisionMetrics.</summary>
public class DecisionMetricsTests
{
    [Theory]
    [InlineData(75, 5, 75.0)]    // expected 75, actual 100 → off by 25 → 75
    [InlineData(80, 2, 60.0)]    // expected 80, actual 40  → off by 40 → 60
    [InlineData(80, 4, 100.0)]   // perfect prediction
    [InlineData(100, 1, 20.0)]   // expected 100, actual 20 → off by 80 → 20
    public void AlignmentScore_MeasuresExpectationAccuracy(int expected, int rating, double want)
    {
        Assert.Equal(want, DecisionMetrics.AlignmentScore(expected, rating), precision: 6);
    }

    [Fact]
    public void AlignmentScore_NeverNegative()
    {
        // expected 100, rating 1 → actual 20 → 100 - 80 = 20 (positive, no floor)
        Assert.Equal(20, DecisionMetrics.AlignmentScore(100, 1), precision: 6);
        // expected 100, rating 5 → actual 100 → perfect alignment 100
        Assert.Equal(100, DecisionMetrics.AlignmentScore(100, 5), precision: 6);
    }

    [Fact]
    public void DecisionPerformanceScore_EmptyReviews_ReturnsZero()
    {
        Assert.Equal(0, DecisionMetrics.DecisionPerformanceScore([]));
    }

    [Fact]
    public void DecisionPerformanceScore_PerfectReview_Scores100()
    {
        // rating 5 → outcome 100; successRate 100; alignment 100 → 0.4·100+0.3·100+0.3·100 = 100
        var score = DecisionMetrics.DecisionPerformanceScore([(5, 100, true)]);
        Assert.Equal(100, score, precision: 1);
    }

    [Fact]
    public void DecisionPerformanceScore_MixedReviews_WeightedCorrectly()
    {
        // Two reviews: (5,80,true) and (2,40,false)
        // avgOutcome = (100+40)/2 = 70; successRate = 50; avgAlign = (80+100)/2 = 90
        // 0.4·70 + 0.3·50 + 0.3·90 = 28 + 15 + 27 = 70
        var score = DecisionMetrics.DecisionPerformanceScore([(5, 80, true), (2, 40, false)]);
        Assert.Equal(70.0, score, precision: 1);
    }

    [Fact]
    public void DecisionPerformanceScore_SuccessRequiresRatingThreeAndWouldChooseAgain()
    {
        // rating 5 but wouldChooseAgain=false → not successful
        // avgOutcome=100, successRate=0, alignment=100 → 40+0+30 = 70
        var score = DecisionMetrics.DecisionPerformanceScore([(5, 100, false)]);
        Assert.Equal(70.0, score, precision: 1);
    }
}
