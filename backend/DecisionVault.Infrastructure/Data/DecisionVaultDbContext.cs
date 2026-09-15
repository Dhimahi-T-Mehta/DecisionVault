using DecisionVault.Domain;
using DecisionVault.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DecisionVault.Infrastructure.Data;

public class DecisionVaultDbContext(DbContextOptions<DecisionVaultDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Decision> Decisions => Set<Decision>();
    public DbSet<DecisionOption> DecisionOptions => Set<DecisionOption>();
    public DbSet<DecisionReason> DecisionReasons => Set<DecisionReason>();
    public DbSet<DecisionReview> DecisionReviews => Set<DecisionReview>();
    public DbSet<DecisionEvent> DecisionEvents => Set<DecisionEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("decision_vault");

        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users");
            e.Property(u => u.FullName).HasMaxLength(100).IsRequired();
            e.Property(u => u.Email).HasMaxLength(255).IsRequired();
            e.Property(u => u.PasswordHash).HasMaxLength(100).IsRequired();
            e.Property(u => u.Role).HasMaxLength(20).IsRequired();
            e.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<Category>(e =>
        {
            e.ToTable("categories");
            e.Property(c => c.Name).HasMaxLength(60).IsRequired();
            e.HasIndex(c => c.Name).IsUnique();
            e.Property(c => c.Kind).HasConversion<string>().HasMaxLength(20);
            e.Property(c => c.Description).HasMaxLength(300);
        });

        modelBuilder.Entity<Decision>(e =>
        {
            e.ToTable("decisions");
            e.Property(d => d.Title).HasMaxLength(200).IsRequired();
            e.Property(d => d.Description).HasMaxLength(2000);
            e.Property(d => d.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(d => d.ExpectedOutcome).HasMaxLength(1000);
            e.Property(d => d.ConfidenceScore).HasColumnName("confidence_score");

            e.HasOne(d => d.User)
                .WithMany(u => u.Decisions)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(d => d.Category)
                .WithMany(c => c.Decisions)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(d => d.SelectedOption)
                .WithMany()
                .HasForeignKey(d => d.SelectedOptionId)
                .OnDelete(DeleteBehavior.SetNull);

            // Owner-scoped list queries: WHERE UserId AND Status ORDER BY CreatedAt DESC.
            e.HasIndex(d => new { d.UserId, d.Status, d.CreatedAt });
            // Analytics grouping by category within a user's vault.
            e.HasIndex(d => new { d.UserId, d.CategoryId });
            // Review-date range filters and upcoming-review queries.
            e.HasIndex(d => new { d.UserId, d.ReviewDate });
        });

        modelBuilder.Entity<DecisionOption>(e =>
        {
            e.ToTable("decision_options");
            e.Property(o => o.Name).HasMaxLength(100).IsRequired();
            e.Property(o => o.Description).HasMaxLength(500);
            e.Property(o => o.Advantages).HasMaxLength(500);
            e.Property(o => o.Disadvantages).HasMaxLength(500);

            e.HasOne(o => o.Decision)
                .WithMany(d => d.Options)
                .HasForeignKey(o => o.DecisionId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(o => new { o.DecisionId, o.Name }).IsUnique();
        });

        modelBuilder.Entity<DecisionReason>(e =>
        {
            e.ToTable("decision_reasons");
            e.Property(r => r.Type).HasConversion<string>().HasMaxLength(10);
            e.Property(r => r.Category).HasMaxLength(60).IsRequired();
            e.Property(r => r.Text).HasMaxLength(500).IsRequired();

            e.HasOne(r => r.Decision)
                .WithMany(d => d.Reasons)
                .HasForeignKey(r => r.DecisionId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(r => r.DecisionId);
        });

        modelBuilder.Entity<DecisionReview>(e =>
        {
            e.ToTable("decision_reviews");
            e.Property(r => r.ActualOutcome).HasMaxLength(1000).IsRequired();
            e.Property(r => r.WhatWentWell).HasMaxLength(500);
            e.Property(r => r.WhatWentWrong).HasMaxLength(500);
            e.Property(r => r.LessonsLearned).HasMaxLength(500);

            e.HasOne(r => r.Decision)
                .WithOne(d => d.Review)
                .HasForeignKey<DecisionReview>(r => r.DecisionId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(r => r.DecisionId).IsUnique();
            e.Property(r => r.OutcomeRating).HasColumnName("outcome_rating");
        });

        modelBuilder.Entity<DecisionEvent>(e =>
        {
            e.ToTable("decision_events");
            e.Property(ev => ev.EventType).HasMaxLength(40).IsRequired();
            e.Property(ev => ev.Description).HasMaxLength(300).IsRequired();

            e.HasOne(ev => ev.Decision)
                .WithMany(d => d.Events)
                .HasForeignKey(ev => ev.DecisionId)
                .OnDelete(DeleteBehavior.Cascade);

            // Timeline: newest first per decision.
            e.HasIndex(ev => new { ev.DecisionId, ev.CreatedAt });
        });
    }
}
