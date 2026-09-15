using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DecisionVault.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "decision_vault");

            migrationBuilder.CreateTable(
                name: "categories",
                schema: "decision_vault",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "decision_vault",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FullName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "decision_events",
                schema: "decision_vault",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DecisionId = table.Column<int>(type: "integer", nullable: false),
                    EventType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_decision_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "decision_options",
                schema: "decision_vault",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DecisionId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Advantages = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Disadvantages = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Score = table.Column<int>(type: "integer", nullable: true),
                    Weight = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_decision_options", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "decisions",
                schema: "decision_vault",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    CategoryId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DecisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    confidence_score = table.Column<int>(type: "integer", nullable: true),
                    ExpectedSuccessScore = table.Column<int>(type: "integer", nullable: true),
                    ExpectedOutcome = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SelectedOptionId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_decisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_decisions_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "decision_vault",
                        principalTable: "categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_decisions_decision_options_SelectedOptionId",
                        column: x => x.SelectedOptionId,
                        principalSchema: "decision_vault",
                        principalTable: "decision_options",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_decisions_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "decision_vault",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "decision_reasons",
                schema: "decision_vault",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DecisionId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Category = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_decision_reasons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_decision_reasons_decisions_DecisionId",
                        column: x => x.DecisionId,
                        principalSchema: "decision_vault",
                        principalTable: "decisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "decision_reviews",
                schema: "decision_vault",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DecisionId = table.Column<int>(type: "integer", nullable: false),
                    ActualOutcome = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    outcome_rating = table.Column<int>(type: "integer", nullable: false),
                    WhatWentWell = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    WhatWentWrong = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LessonsLearned = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    WouldChooseAgain = table.Column<bool>(type: "boolean", nullable: false),
                    IsSuccessful = table.Column<bool>(type: "boolean", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_decision_reviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_decision_reviews_decisions_DecisionId",
                        column: x => x.DecisionId,
                        principalSchema: "decision_vault",
                        principalTable: "decisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_categories_Name",
                schema: "decision_vault",
                table: "categories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_decision_events_DecisionId_CreatedAt",
                schema: "decision_vault",
                table: "decision_events",
                columns: new[] { "DecisionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_decision_options_DecisionId_Name",
                schema: "decision_vault",
                table: "decision_options",
                columns: new[] { "DecisionId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_decision_reasons_DecisionId",
                schema: "decision_vault",
                table: "decision_reasons",
                column: "DecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_decision_reviews_DecisionId",
                schema: "decision_vault",
                table: "decision_reviews",
                column: "DecisionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_decisions_CategoryId",
                schema: "decision_vault",
                table: "decisions",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_decisions_SelectedOptionId",
                schema: "decision_vault",
                table: "decisions",
                column: "SelectedOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_decisions_UserId_CategoryId",
                schema: "decision_vault",
                table: "decisions",
                columns: new[] { "UserId", "CategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_decisions_UserId_ReviewDate",
                schema: "decision_vault",
                table: "decisions",
                columns: new[] { "UserId", "ReviewDate" });

            migrationBuilder.CreateIndex(
                name: "IX_decisions_UserId_Status_CreatedAt",
                schema: "decision_vault",
                table: "decisions",
                columns: new[] { "UserId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_users_Email",
                schema: "decision_vault",
                table: "users",
                column: "Email",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_decision_events_decisions_DecisionId",
                schema: "decision_vault",
                table: "decision_events",
                column: "DecisionId",
                principalSchema: "decision_vault",
                principalTable: "decisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_decision_options_decisions_DecisionId",
                schema: "decision_vault",
                table: "decision_options",
                column: "DecisionId",
                principalSchema: "decision_vault",
                principalTable: "decisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_decision_options_decisions_DecisionId",
                schema: "decision_vault",
                table: "decision_options");

            migrationBuilder.DropTable(
                name: "decision_events",
                schema: "decision_vault");

            migrationBuilder.DropTable(
                name: "decision_reasons",
                schema: "decision_vault");

            migrationBuilder.DropTable(
                name: "decision_reviews",
                schema: "decision_vault");

            migrationBuilder.DropTable(
                name: "decisions",
                schema: "decision_vault");

            migrationBuilder.DropTable(
                name: "categories",
                schema: "decision_vault");

            migrationBuilder.DropTable(
                name: "decision_options",
                schema: "decision_vault");

            migrationBuilder.DropTable(
                name: "users",
                schema: "decision_vault");
        }
    }
}
