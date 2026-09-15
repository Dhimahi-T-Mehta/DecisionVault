using Microsoft.EntityFrameworkCore;
using DecisionVault.Infrastructure.Data;
using DecisionVault.Domain.Entities;
using DecisionVault.Domain.Enums;

namespace DecisionVault.API.Configuration;

public static class DbSeeder
{
    /// <summary>
    /// Development-only demo data. Demo passwords are documented in README as DEV-ONLY.
    /// Production setups manage users through registration; this idempotently ensures
    /// baseline categories and two demo accounts.
    /// </summary>
    public static async Task SeedAsync(IServiceProvider services, ILogger logger)
    {
        var config = services.GetRequiredService<IConfiguration>();
        if (!config.GetValue<bool>("SeedDemoData", false))
            return;

        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DecisionVaultDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<Application.Interfaces.IPasswordHasher>();

        await context.Database.MigrateAsync();

        if (!await context.Set<Category>().AnyAsync())
        {
            context.Set<Category>().AddRange(
                new Category { Name = "Career", Kind = CategoryKind.Career, Description = "Jobs, roles, professional moves", CreatedAt = DateTime.UtcNow },
                new Category { Name = "Education", Kind = CategoryKind.Education, Description = "Learning and study choices", CreatedAt = DateTime.UtcNow },
                new Category { Name = "Finance", Kind = CategoryKind.Financial, Description = "Money, investing, purchases", CreatedAt = DateTime.UtcNow },
                new Category { Name = "Health", Kind = CategoryKind.Health, Description = "Wellbeing and lifestyle", CreatedAt = DateTime.UtcNow },
                new Category { Name = "Technology", Kind = CategoryKind.Technology, Description = "Tools, stacks, platforms", CreatedAt = DateTime.UtcNow },
                new Category { Name = "Personal", Kind = CategoryKind.Personal, Description = "Everything else that matters", CreatedAt = DateTime.UtcNow });
            await context.SaveChangesAsync();
            logger.LogInformation("Seeded default categories");
        }

        var categories = await context.Set<Category>().ToDictionaryAsync(c => c.Name, c => c.Id);

        async Task EnsureUserAsync(string email, string fullName, string role, string password)
        {
            var users = context.Set<User>();
            if (await users.AnyAsync(u => u.Email == email))
                return;
            users.Add(new User
            {
                FullName = fullName,
                Email = email,
                PasswordHash = hasher.Hash(password),
                Role = role,
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-30)
            });
            await context.SaveChangesAsync();
            logger.LogInformation("Seeded {Role} account {Email}", role, email);
        }

        // DEV-ONLY demo credentials, documented in README.
        await EnsureUserAsync("admin@example.com", "Vault Administrator", "Admin", "Admin#12345");
        await EnsureUserAsync("demo@example.com", "Demo Decision Maker", "User", "Demo#12345");

        var demo = await context.Set<User>().FirstAsync(u => u.Email == "demo@example.com");
        if (await context.Set<Decision>().AnyAsync(d => d.UserId == demo.Id))
            return;

        var decisions = BuildDemoDecisions(categories, demo.Id);
        context.Set<Decision>().AddRange(decisions);
        await context.SaveChangesAsync();

        // Second pass: decision→selected-option FK cycles with option→decision,
        // so selections are linked only after both sides exist.
        var selections = new Dictionary<string, string>
        {
            ["Which cloud platform should I learn first?"] = "AWS",
            ["Accept the startup offer or stay in current role?"] = "Join the startup",
            ["Which laptop for the next three years?"] = "MacBook Pro 14",
            ["Part-time MSc or certification path this year?"] = "Part-time MSc"
        };
        foreach (var decision in decisions)
        {
            if (selections.TryGetValue(decision.Title, out var optionName))
                decision.SelectedOptionId = decision.Options.First(o => o.Name == optionName).Id;
        }
        await context.SaveChangesAsync();
    }

    private static List<Decision> BuildDemoDecisions(Dictionary<string, int> categories, int userId) =>
    [
        BuildTechDecision(categories["Technology"], userId),
        BuildCareerDecision(categories["Career"], userId),
        BuildFinanceDecision(categories["Finance"], userId),
        BuildEducationDecision(categories["Education"], userId),
        BuildPendingDecision(categories["Health"], userId)
    ];

    private static Decision BuildTechDecision(int categoryId, int userId)
    {
        var aws = new DecisionOption { Name = "AWS", Description = "Largest market share", Advantages = "More job listings; mature docs", Disadvantages = "Certification path is broad", Score = 8, Weight = 8, CreatedAt = DateTime.UtcNow.AddDays(-20) };
        var azure = new DecisionOption { Name = "Azure", Description = "Strong in enterprises", Advantages = "Fits .NET background", Disadvantages = "Fewer startup roles", Score = 7, Weight = 7, CreatedAt = DateTime.UtcNow.AddDays(-20) };
        var gcp = new DecisionOption { Name = "GCP", Description = "Data-focused stack", Advantages = "Great data tooling", Disadvantages = "Smaller market share", Score = 6, Weight = 6, CreatedAt = DateTime.UtcNow.AddDays(-20) };

        var decision = new Decision
        {
            UserId = userId,
            CategoryId = categoryId,
            Title = "Which cloud platform should I learn first?",
            Description = "Choosing a primary cloud provider to anchor backend skills and portfolio projects.",
            Status = DecisionStatus.Reviewed,
            DecisionDate = DateTime.UtcNow.AddDays(-18),
            ReviewDate = DateTime.UtcNow.AddDays(-2),
            ConfidenceScore = 80,
            ExpectedSuccessScore = 85,
            ExpectedOutcome = "Build two deployed projects and speak fluently about cloud architecture in interviews.",
            CreatedAt = DateTime.UtcNow.AddDays(-21),
            UpdatedAt = DateTime.UtcNow.AddDays(-2),
            Options = [aws, azure, gcp],
            Reasons =
            [
                new() { Type = ReasonType.Pro, Category = "Career", Text = "Most job postings mention AWS experience.", CreatedAt = DateTime.UtcNow.AddDays(-20) },
                new() { Type = ReasonType.Pro, Category = "Long-term benefit", Text = "Cloud skills compound across every future project.", CreatedAt = DateTime.UtcNow.AddDays(-20) },
                new() { Type = ReasonType.Con, Category = "Time", Text = "Certification prep could eat a full month.", CreatedAt = DateTime.UtcNow.AddDays(-20) },
                new() { Type = ReasonType.Note, Category = "Personal preference", Text = "Prefer platforms with generous free tiers.", CreatedAt = DateTime.UtcNow.AddDays(-19) }
            ],
            Events =
            [
                new() { EventType = "Created", Description = "Decision created", CreatedAt = DateTime.UtcNow.AddDays(-21) },
                new() { EventType = "OptionAdded", Description = "Options AWS, Azure, GCP added", CreatedAt = DateTime.UtcNow.AddDays(-20) },
                new() { EventType = "ReasonAdded", Description = "Reasoning recorded", CreatedAt = DateTime.UtcNow.AddDays(-20) },
                new() { EventType = "Finalized", Description = "Decision finalized with confidence and expected outcome", CreatedAt = DateTime.UtcNow.AddDays(-18) },
                new() { EventType = "StatusChanged", Description = "Status changed to InProgress", CreatedAt = DateTime.UtcNow.AddDays(-17) },
                new() { EventType = "StatusChanged", Description = "Status changed to ReadyForReview", CreatedAt = DateTime.UtcNow.AddDays(-3) },
                new() { EventType = "Reviewed", Description = "Review recorded — outcome rated 5/5", CreatedAt = DateTime.UtcNow.AddDays(-2) }
            ]
        };

        decision.Review = new DecisionReview
        {
            DecisionId = decision.Id,
            ActualOutcome = "Deployed a .NET API on AWS with RDS and S3; passed the Solutions Architect Associate exam.",
            OutcomeRating = 5,
            WhatWentWell = "Free tier covered all learning projects; documentation quality exceeded expectations.",
            WhatWentWrong = "IAM setup had a steep learning curve in week one.",
            LessonsLearned = "Picking the platform with the largest ecosystem paid off for interview prep.",
            WouldChooseAgain = true,
            IsSuccessful = true,
            ReviewedAt = DateTime.UtcNow.AddDays(-2),
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        };
        return decision;
    }

    private static Decision BuildCareerDecision(int categoryId, int userId)
    {
        var startup = new DecisionOption { Name = "Join the startup", Advantages = "Equity, fast growth, broad ownership", Disadvantages = "Lower initial salary, uncertainty", Score = 7, Weight = 8, CreatedAt = DateTime.UtcNow.AddDays(-40) };
        var corporate = new DecisionOption { Name = "Stay corporate", Advantages = "Stability, structured mentoring", Disadvantages = "Slower growth, narrow scope", Score = 6, Weight = 7, CreatedAt = DateTime.UtcNow.AddDays(-40) };

        var decision = new Decision
        {
            UserId = userId,
            CategoryId = categoryId,
            Title = "Accept the startup offer or stay in current role?",
            Description = "A fintech startup offered a senior position with equity; current role is stable but plateauing.",
            Status = DecisionStatus.Reviewed,
            DecisionDate = DateTime.UtcNow.AddDays(-38),
            ReviewDate = DateTime.UtcNow.AddDays(-8),
            ConfidenceScore = 65,
            ExpectedSuccessScore = 70,
            ExpectedOutcome = "Faster professional growth and a meaningful jump in end-to-end ownership within six months.",
            CreatedAt = DateTime.UtcNow.AddDays(-41),
            UpdatedAt = DateTime.UtcNow.AddDays(-8),
            Options = [startup, corporate],
            Reasons =
            [
                new() { Type = ReasonType.Pro, Category = "Opportunity", Text = "Chance to own the whole product surface.", CreatedAt = DateTime.UtcNow.AddDays(-40) },
                new() { Type = ReasonType.Con, Category = "Risk", Text = "Runway is about 18 months.", CreatedAt = DateTime.UtcNow.AddDays(-40) }
            ],
            Events =
            [
                new() { EventType = "Created", Description = "Decision created", CreatedAt = DateTime.UtcNow.AddDays(-41) },
                new() { EventType = "OptionAdded", Description = "Option 'Join the startup' added", CreatedAt = DateTime.UtcNow.AddDays(-40) },
                new() { EventType = "OptionAdded", Description = "Option 'Stay corporate' added", CreatedAt = DateTime.UtcNow.AddDays(-40) },
                new() { EventType = "OptionSelected", Description = "Option 'Join the startup' marked as intended choice", CreatedAt = DateTime.UtcNow.AddDays(-38) },
                new() { EventType = "StatusChanged", Description = "Status changed to Decided", CreatedAt = DateTime.UtcNow.AddDays(-38) },
                new() { EventType = "Finalized", Description = "Decision finalized with confidence and expected outcome", CreatedAt = DateTime.UtcNow.AddDays(-38) },
                new() { EventType = "StatusChanged", Description = "Status changed to InProgress", CreatedAt = DateTime.UtcNow.AddDays(-30) },
                new() { EventType = "StatusChanged", Description = "Status changed to ReadyForReview", CreatedAt = DateTime.UtcNow.AddDays(-10) },
                new() { EventType = "StatusChanged", Description = "Status changed to Reviewed", CreatedAt = DateTime.UtcNow.AddDays(-8) },
                new() { EventType = "Reviewed", Description = "Review recorded — outcome rated 4/5", CreatedAt = DateTime.UtcNow.AddDays(-8) }
            ]
        };

        decision.Review = new DecisionReview
        {
            DecisionId = decision.Id,
            ActualOutcome = "Shipped the payments module in month two; ownership broad as expected, workload heavy.",
            OutcomeRating = 4,
            WhatWentWell = "Growth was real — led a team of three by month four.",
            WhatWentWrong = "Underestimated on-call burden.",
            LessonsLearned = "Weigh operational load, not just feature work, when comparing offers.",
            WouldChooseAgain = true,
            IsSuccessful = true,
            ReviewedAt = DateTime.UtcNow.AddDays(-8),
            CreatedAt = DateTime.UtcNow.AddDays(-8)
        };
        return decision;
    }

    private static Decision BuildFinanceDecision(int categoryId, int userId)
    {
        var laptop = new DecisionOption { Name = "MacBook Pro 14", Advantages = "Battery life, build quality", Disadvantages = "Price", Score = 8, Weight = 6, CreatedAt = DateTime.UtcNow.AddDays(-70) };
        var thinkpad = new DecisionOption { Name = "ThinkPad X1", Advantages = "Linux support, repairable", Disadvantages = "Heavier", Score = 7, Weight = 6, CreatedAt = DateTime.UtcNow.AddDays(-70) };

        var decision = new Decision
        {
            UserId = userId,
            CategoryId = categoryId,
            Title = "Which laptop for the next three years?",
            Description = "Daily dev machine replacement; budget around the same for both options.",
            Status = DecisionStatus.Reviewed,
            DecisionDate = DateTime.UtcNow.AddDays(-68),
            ReviewDate = DateTime.UtcNow.AddDays(-25),
            ConfidenceScore = 75,
            ExpectedSuccessScore = 80,
            ExpectedOutcome = "Quiet, fast dev machine that lasts through the master's degree.",
            CreatedAt = DateTime.UtcNow.AddDays(-71),
            UpdatedAt = DateTime.UtcNow.AddDays(-25),
            Options = [laptop, thinkpad],
            Reasons =
            [
                new() { Type = ReasonType.Pro, Category = "Long-term benefit", Text = "Three-year lifespan amortizes the price.", CreatedAt = DateTime.UtcNow.AddDays(-70) },
                new() { Type = ReasonType.Con, Category = "Financial", Text = "20% more expensive than the alternative.", CreatedAt = DateTime.UtcNow.AddDays(-70) }
            ],
            Events =
            [
                new() { EventType = "Created", Description = "Decision created", CreatedAt = DateTime.UtcNow.AddDays(-71) },
                new() { EventType = "OptionAdded", Description = "Option 'MacBook Pro 14' added", CreatedAt = DateTime.UtcNow.AddDays(-70) },
                new() { EventType = "OptionAdded", Description = "Option 'ThinkPad X1' added", CreatedAt = DateTime.UtcNow.AddDays(-70) },
                new() { EventType = "OptionSelected", Description = "Option 'MacBook Pro 14' marked as intended choice", CreatedAt = DateTime.UtcNow.AddDays(-68) },
                new() { EventType = "StatusChanged", Description = "Status changed to Decided", CreatedAt = DateTime.UtcNow.AddDays(-68) },
                new() { EventType = "Finalized", Description = "Decision finalized with confidence and expected outcome", CreatedAt = DateTime.UtcNow.AddDays(-68) },
                new() { EventType = "StatusChanged", Description = "Status changed to InProgress", CreatedAt = DateTime.UtcNow.AddDays(-50) },
                new() { EventType = "StatusChanged", Description = "Status changed to ReadyForReview", CreatedAt = DateTime.UtcNow.AddDays(-30) },
                new() { EventType = "StatusChanged", Description = "Status changed to Reviewed", CreatedAt = DateTime.UtcNow.AddDays(-25) },
                new() { EventType = "Reviewed", Description = "Review recorded — outcome rated 2/5", CreatedAt = DateTime.UtcNow.AddDays(-25) }
            ]
        };

        decision.Review = new DecisionReview
        {
            DecisionId = decision.Id,
            ActualOutcome = "Keyboard developed a rattle; RAM not upgradeable so 16GB now feels tight.",
            OutcomeRating = 2,
            WhatWentWell = "Battery life is as advertised.",
            WhatWentWrong = "Non-upgradeable RAM was a real limitation for VM work.",
            LessonsLearned = "For laptops, prioritise serviceability over polish.",
            WouldChooseAgain = false,
            IsSuccessful = false,
            ReviewedAt = DateTime.UtcNow.AddDays(-25),
            CreatedAt = DateTime.UtcNow.AddDays(-25)
        };
        return decision;
    }

    private static Decision BuildEducationDecision(int categoryId, int userId)
    {
        var msc = new DecisionOption { Name = "Part-time MSc", Advantages = "Credential + network", Disadvantages = "Two evenings a week for 2 years", Score = 7, Weight = 8, CreatedAt = DateTime.UtcNow.AddDays(-10) };
        var certs = new DecisionOption { Name = "Certifications only", Advantages = "Cheaper, self-paced", Disadvantages = "Less depth, less signal", Score = 6, Weight = 7, CreatedAt = DateTime.UtcNow.AddDays(-10) };

        return new Decision
        {
            UserId = userId,
            CategoryId = categoryId,
            Title = "Part-time MSc or certification path this year?",
            Description = "Weighing formal education against targeted certifications for the next career step.",
            Status = DecisionStatus.Decided,
            DecisionDate = DateTime.UtcNow.AddDays(-8),
            ConfidenceScore = 70,
            ExpectedSuccessScore = 75,
            ExpectedOutcome = "Structured progression without burning out alongside work.",
            CreatedAt = DateTime.UtcNow.AddDays(-11),
            UpdatedAt = DateTime.UtcNow.AddDays(-8),
            Options = [msc, certs],
            Reasons =
            [
                new() { Type = ReasonType.Pro, Category = "Education", Text = "Formal theory fills gaps self-study keeps exposing.", CreatedAt = DateTime.UtcNow.AddDays(-10) },
                new() { Type = ReasonType.Con, Category = "Time", Text = "Two evenings a week is significant.", CreatedAt = DateTime.UtcNow.AddDays(-10) }
            ],
            Events =
            [
                new() { EventType = "Created", Description = "Decision created", CreatedAt = DateTime.UtcNow.AddDays(-11) },
                new() { EventType = "Finalized", Description = "Decision finalized with confidence and expected outcome", CreatedAt = DateTime.UtcNow.AddDays(-8) }
            ]
        };
    }

    private static Decision BuildPendingDecision(int categoryId, int userId)
    {
        var gym = new DecisionOption { Name = "Evening gym plan", Advantages = "Social, structured", Disadvantages = "Commute after work", Score = 6, Weight = 5, CreatedAt = DateTime.UtcNow.AddDays(-5) };
        var home = new DecisionOption { Name = "Home calisthenics", Advantages = "Zero commute, flexible", Disadvantages = "Self-discipline required", Score = 7, Weight = 5, CreatedAt = DateTime.UtcNow.AddDays(-5) };

        return new Decision
        {
            UserId = userId,
            CategoryId = categoryId,
            Title = "How to build a sustainable fitness routine?",
            Description = "Comparing a commercial gym membership against a home routine for consistency.",
            Status = DecisionStatus.Evaluating,
            CreatedAt = DateTime.UtcNow.AddDays(-6),
            UpdatedAt = DateTime.UtcNow.AddDays(-5),
            Options = [gym, home],
            Reasons =
            [
                new() { Type = ReasonType.Note, Category = "Time", Text = "Weeknights are the constraint to design around.", CreatedAt = DateTime.UtcNow.AddDays(-5) }
            ],
            Events =
            [
                new() { EventType = "Created", Description = "Decision created", CreatedAt = DateTime.UtcNow.AddDays(-6) },
                new() { EventType = "StatusChanged", Description = "Status changed to Evaluating", CreatedAt = DateTime.UtcNow.AddDays(-5) }
            ]
        };
    }
}
