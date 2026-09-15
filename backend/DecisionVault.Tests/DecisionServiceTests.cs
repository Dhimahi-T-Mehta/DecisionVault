using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using DecisionVault.Application.DTOs;
using DecisionVault.Application.Services;
using DecisionVault.Infrastructure.Data;
using DecisionVault.Domain.Entities;
using DecisionVault.Domain.Enums;
using DecisionVault.Domain.Exceptions;

namespace DecisionVault.Tests;

/// <summary>
/// DecisionService business rules against the EF InMemory provider — real query
/// pipeline (Includes, relationship fixup), not a mocked LINQ facade.
/// </summary>
public class DecisionServiceTests : IClassFixture<ServiceFixture>
{
    private readonly ServiceFixture _fx;
    public DecisionServiceTests(ServiceFixture fx) => _fx = fx;

    private Decision SeedDecision(int options = 0, DecisionStatus status = DecisionStatus.Draft)
    {
        var decision = new Decision
        {
            UserId = _fx.DemoUser.Id,
            CategoryId = _fx.Career.Id,
            Title = "Test decision",
            Status = status,
            CreatedAt = DateTime.UtcNow
        };
        for (var i = 1; i <= options; i++)
            decision.Options.Add(new DecisionOption { Name = $"Option {i}", Weight = 0, CreatedAt = DateTime.UtcNow });
        _fx.Db.Decisions.Add(decision);
        _fx.Db.SaveChanges();
        _fx.Db.ChangeTracker.Clear();
        return decision;
    }

    [Fact]
    public async Task Create_ValidRequest_StartsAsDraft_AndLogsCreatedEvent()
    {
        var svc = new DecisionService(_fx.NewUoW(), NullLogger<DecisionService>.Instance);
        var dto = await svc.CreateAsync(_fx.DemoUser.Id, new DecisionCreateRequest("Cloud or on-prem?", "Live in 6 months", _fx.Career.Id, null, null, 80, 75, null));

        Assert.Equal(DecisionStatus.Draft.ToString(), dto.Status);
        var events = _fx.Db.DecisionEvents.Where(e => e.DecisionId == dto.Id).ToList();
        Assert.Contains(events, e => e.EventType == EventTypes.Created);
    }

    [Fact]
    public async Task Create_InvalidConfidence_ThrowsValidationWithAllErrors()
    {
        var svc = new DecisionService(_fx.NewUoW(), NullLogger<DecisionService>.Instance);
        var ex = await Assert.ThrowsAsync<DecisionVault.Domain.Exceptions.ValidationException>(() =>
            svc.CreateAsync(_fx.DemoUser.Id, new DecisionCreateRequest(
                "Bad confidence", Description: null, CategoryId: _fx.Career.Id, DecisionDate: null,
                ReviewDate: null, ConfidenceScore: 0, ExpectedSuccessScore: 150, ExpectedOutcome: null)));
        var agg = ex.Message + string.Join(';', ex.Errors ?? []);
        Assert.Contains("Confidence", agg);
        Assert.Contains("Expected success", agg);
    }

    [Fact]
    public async Task Create_UnknownCategory_ThrowsValidation()
    {
        var svc = new DecisionService(_fx.NewUoW(), NullLogger<DecisionService>.Instance);
        await Assert.ThrowsAsync<DecisionVault.Domain.Exceptions.ValidationException>(() =>
            svc.CreateAsync(_fx.DemoUser.Id, new DecisionCreateRequest("Orphan decision", Description: null, CategoryId: 9999, DecisionDate: null, ReviewDate: null, ConfidenceScore: 70, ExpectedSuccessScore: 70, ExpectedOutcome: null)));
    }

    [Fact]
    public async Task GetById_OtherUsersDecision_ThrowsForbidden_ForNonAdmin()
    {
        var svc = new DecisionService(_fx.NewUoW(), NullLogger<DecisionService>.Instance);
        var dto = await svc.CreateAsync(_fx.DemoUser.Id, new DecisionCreateRequest("Secret plan", null, _fx.Career.Id, null, null, 70, 70, null));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            svc.GetByIdForUserAsync(dto.Id, userId: _fx.AdminUser.Id, isAdmin: false));
        // Admin can read it
        var asAdmin = await svc.GetByIdForUserAsync(dto.Id, userId: _fx.AdminUser.Id, isAdmin: true);
        Assert.Equal(dto.Id, asAdmin.Id);
    }

    [Fact]
    public async Task Finalize_RequiresTwoOptions_AndSelectedOption()
    {
        var svc = new DecisionService(_fx.NewUoW(), NullLogger<DecisionService>.Instance);
        var d = SeedDecision(options: 1);

        // One option only → validation error
        var ex = await Assert.ThrowsAsync<DecisionVault.Domain.Exceptions.ValidationException>(() =>
            svc.FinalizeAsync(d.Id, _fx.DemoUser.Id, new FinalizeRequest(d.Options.First().Id, 70, 75, "Expect it")));
        Assert.Contains("two options", ex.Message + string.Join(';', ex.Errors ?? []));
    }

    [Fact]
    public async Task Mutations_ByOtherUser_ThrowForbidden()
    {
        var svc = new DecisionService(_fx.NewUoW(), NullLogger<DecisionService>.Instance);
        var dto = await svc.CreateAsync(_fx.DemoUser.Id,
            new DecisionCreateRequest("Expect it", null, _fx.Career.Id, null, null, 70, 70, null));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            svc.UpdateAsync(dto.Id, _fx.AdminUser.Id,
                new DecisionUpdateRequest("Hijacked", null, _fx.Career.Id, null, null, 70, 70, null)));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            svc.AddOptionAsync(dto.Id, _fx.AdminUser.Id, new DecisionOptionRequest("X", null, null, null, Score: 5, Weight: 5)));
        await Assert.ThrowsAsync<ForbiddenException>(() => svc.DeleteAsync(dto.Id, _fx.AdminUser.Id));
    }

    [Fact]
    public async Task Transition_DecidedToEvaluating_ClearsSelectedOption()
    {
        var svc = new DecisionService(_fx.NewUoW(), NullLogger<DecisionService>.Instance);
        var d = SeedDecision(options: 2, status: DecisionStatus.Draft);
        await svc.TransitionAsync(d.Id, _fx.DemoUser.Id, new TransitionRequest(DecisionStatus.Evaluating.ToString()));
        var optionId = _fx.Db.DecisionOptions.Where(o => o.DecisionId == d.Id).First().Id;
        await svc.SelectOptionAsync(d.Id, _fx.DemoUser.Id, new SelectOptionRequest(optionId)); // → Decided

        // Re-open: back to Evaluating; the selection must not survive.
        await svc.TransitionAsync(d.Id, _fx.DemoUser.Id, new TransitionRequest(DecisionStatus.Evaluating.ToString()));
        var reloaded = await svc.GetByIdForUserAsync(d.Id, _fx.DemoUser.Id, isAdmin: false);
        Assert.Equal(DecisionStatus.Evaluating.ToString(), reloaded.Status);
        Assert.Null(reloaded.SelectedOptionId);
    }


    [Fact]
    public async Task Finalize_WithTwoOptions_TransitionsToDecided_AndSetsExpectations()
    {
        var svc = new DecisionService(_fx.NewUoW(), NullLogger<DecisionService>.Instance);
        var d = SeedDecision(options: 2);
        var optionId = _fx.Db.DecisionOptions.Where(o => o.DecisionId == d.Id).First().Id;
        var dto = await svc.FinalizeAsync(d.Id, _fx.DemoUser.Id, new FinalizeRequest(optionId, 72, 88, "Delivered on time"));
        Assert.Equal(DecisionStatus.Decided.ToString(), dto.Status);
        Assert.Equal(optionId, dto.SelectedOptionId);
        Assert.Equal(72, dto.ConfidenceScore);
        Assert.Equal(88, dto.ExpectedSuccessScore);
        var events = _fx.Db.DecisionEvents.Where(e => e.DecisionId == d.Id).ToList();
        Assert.Contains(events, e => e.EventType == EventTypes.OptionSelected);
        Assert.Contains(events, e => e.EventType == EventTypes.Finalized);
    }

    [Fact]
    public async Task Finalize_OptionFromAnotherDecision_ThrowsNotFound()
    {
        var svc = new DecisionService(_fx.NewUoW(), NullLogger<DecisionService>.Instance);
        var d1 = SeedDecision(options: 2);
        var other = SeedDecision(options: 1);
        var foreignOptionId = _fx.Db.DecisionOptions.Where(o => o.DecisionId == other.Id).First().Id;

        await Assert.ThrowsAsync<NotFoundException>(() =>
            svc.FinalizeAsync(d1.Id, _fx.DemoUser.Id, new FinalizeRequest(foreignOptionId, 70, 75, "x")));
    }

    [Fact]
    public async Task SubmitReview_OnlyFromReadyForReview()
    {
        var svc = new DecisionService(_fx.NewUoW(), NullLogger<DecisionService>.Instance);
        var d = SeedDecision(); // Draft

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            svc.SubmitReviewAsync(d.Id, _fx.DemoUser.Id, new DecisionReviewRequest("It worked", 4, null, null, null, true)));
        Assert.Contains("ReadyForReview", ex.Message);
    }

    [Fact]
    public async Task SubmitReview_ComputesIsSuccessful_AndLocksState()
    {
        var svc = new DecisionService(_fx.NewUoW(), NullLogger<DecisionService>.Instance);
        var d = SeedDecision(options: 2);
        var optionId = _fx.Db.DecisionOptions.Where(o => o.DecisionId == d.Id).First().Id;
        await svc.FinalizeAsync(d.Id, _fx.DemoUser.Id, new FinalizeRequest(optionId, 80, 90, "Shipped"));
        await svc.TransitionAsync(d.Id, _fx.DemoUser.Id, new TransitionRequest(DecisionStatus.InProgress.ToString()));
        await svc.TransitionAsync(d.Id, _fx.DemoUser.Id, new TransitionRequest(DecisionStatus.ReadyForReview.ToString()));

        var review = await svc.SubmitReviewAsync(d.Id, _fx.DemoUser.Id,
            new DecisionReviewRequest("Outcome happened", OutcomeRating: 5, WhatWentWell: "All good", WhatWentWrong: null, LessonsLearned: "Plan early", WouldChooseAgain: true));

        Assert.True(review.IsSuccessful);
        Assert.Equal(DecisionStatus.Reviewed.ToString(),
            (await svc.GetByIdForUserAsync(d.Id, _fx.DemoUser.Id, false)).Status);

        // Double review → conflict
        await Assert.ThrowsAsync<ConflictException>(() =>
            svc.SubmitReviewAsync(d.Id, _fx.DemoUser.Id, new DecisionReviewRequest("Again", 3, null, null, null, false)));
    }

    [Fact]
    public async Task SubmitReview_LowRatingWithWouldNotChooseAgain_IsUnsuccessful()
    {
        var svc = new DecisionService(_fx.NewUoW(), NullLogger<DecisionService>.Instance);
        var d = SeedDecision(options: 2);
        var optionId = _fx.Db.DecisionOptions.Where(o => o.DecisionId == d.Id).First().Id;
        await svc.FinalizeAsync(d.Id, _fx.DemoUser.Id, new FinalizeRequest(optionId, 60, 90, "Perfect launch"));
        await svc.TransitionAsync(d.Id, _fx.DemoUser.Id, new TransitionRequest(DecisionStatus.InProgress.ToString()));
        await svc.TransitionAsync(d.Id, _fx.DemoUser.Id, new TransitionRequest(DecisionStatus.ReadyForReview.ToString()));

        // rating 5 but wouldChooseAgain=false → NOT successful (rule: rating>=3 AND wouldChooseAgain)
        var review = await svc.SubmitReviewAsync(d.Id, _fx.DemoUser.Id,
            new DecisionReviewRequest("Worked but would not repeat", 5, null, null, null, WouldChooseAgain: false));
        Assert.False(review.IsSuccessful);
    }

    [Fact]
    public async Task Update_ReviewedDecision_LocksExpectationFields()
    {
        var svc = new DecisionService(_fx.NewUoW(), NullLogger<DecisionService>.Instance);
        var d = SeedDecision(options: 2);
        var optionId = _fx.Db.DecisionOptions.Where(o => o.DecisionId == d.Id).First().Id;
        var finalized = await svc.FinalizeAsync(d.Id, _fx.DemoUser.Id, new FinalizeRequest(optionId, 70, 70, "Expect"));
        await svc.TransitionAsync(d.Id, _fx.DemoUser.Id, new TransitionRequest(DecisionStatus.InProgress.ToString()));
        await svc.TransitionAsync(d.Id, _fx.DemoUser.Id, new TransitionRequest(DecisionStatus.ReadyForReview.ToString()));
        await svc.SubmitReviewAsync(d.Id, _fx.DemoUser.Id, new DecisionReviewRequest("Done", 4, null, null, null, true));

        // Changing confidence after review → conflict
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            svc.UpdateAsync(d.Id, _fx.DemoUser.Id, new DecisionUpdateRequest("Renamed after review", null, _fx.Career.Id, null, null, ConfidenceScore: 90, ExpectedSuccessScore: 70, null)));
        Assert.Contains("after review", ex.Message);

        // Same expectations → allowed (title change only)
        var ok = await svc.UpdateAsync(d.Id, _fx.DemoUser.Id, new DecisionUpdateRequest("Renamed after review", null, _fx.Career.Id, null, null, ConfidenceScore: 70, ExpectedSuccessScore: 70, null));
        Assert.Equal("Renamed after review", ok.Title);
    }

    [Fact]
    public async Task Delete_DecisionWithSelection_ClearsSelectedOption_BeforeRemove()
    {
        var svc = new DecisionService(_fx.NewUoW(), NullLogger<DecisionService>.Instance);
        var d = SeedDecision(options: 2);
        var optionId = _fx.Db.DecisionOptions.Where(o => o.DecisionId == d.Id).First().Id;
        await svc.FinalizeAsync(d.Id, _fx.DemoUser.Id, new FinalizeRequest(optionId, 70, 75, "Expect"));

        await svc.DeleteAsync(d.Id, _fx.DemoUser.Id);
        Assert.False(_fx.Db.Decisions.Any(x => x.Id == d.Id));
    }

    [Fact]
    public async Task Transition_InvalidJump_ThrowsConflict()
    {
        var svc = new DecisionService(_fx.NewUoW(), NullLogger<DecisionService>.Instance);
        var d = SeedDecision(); // Draft
        await Assert.ThrowsAsync<ConflictException>(() =>
            svc.TransitionAsync(d.Id, _fx.DemoUser.Id, new TransitionRequest(DecisionStatus.Reviewed.ToString())));
    }

    [Fact]
    public async Task AddOption_InvalidScoreRange_ThrowsValidation()
    {
        var svc = new DecisionService(_fx.NewUoW(), NullLogger<DecisionService>.Instance);
        var d = SeedDecision();
        await Assert.ThrowsAsync<DecisionVault.Domain.Exceptions.ValidationException>(() =>
            svc.AddOptionAsync(d.Id, _fx.DemoUser.Id, new DecisionOptionRequest("Opt", null, null, null, Score: 42, Weight: 0)));
    }


    [Fact]
    public async Task AddOption_DuplicateName_ThrowsConflict()
    {
        var svc = new DecisionService(_fx.NewUoW(), NullLogger<DecisionService>.Instance);
        var d = SeedDecision(options: 1);
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            svc.AddOptionAsync(d.Id, _fx.DemoUser.Id, new DecisionOptionRequest("option 1", null, null, null, Score: 5, Weight: 5)));
        // Case-insensitive match against the unique (DecisionId, Name) index; 409 not 500.
        Assert.Contains("already exists", ex.Message);
    }

    [Fact]
    public async Task UpdateOption_RenameToSiblingName_ThrowsConflict()
    {
        var svc = new DecisionService(_fx.NewUoW(), NullLogger<DecisionService>.Instance);
        var d = SeedDecision(options: 2);
        var first = d.Options.First();
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            svc.UpdateOptionAsync(d.Id, first.Id, _fx.DemoUser.Id,
                new DecisionOptionRequest(d.Options.Last().Name, null, null, null, Score: 5, Weight: 5)));
        Assert.Contains("already exists", ex.Message);
    }
}
