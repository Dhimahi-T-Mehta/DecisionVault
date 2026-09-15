using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DecisionVault.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCheckConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_decisions_confidence_range",
                schema: "decision_vault",
                table: "decisions",
                sql: "\"confidence_score\" BETWEEN 1 AND 100");

            migrationBuilder.AddCheckConstraint(
                name: "CK_decisions_expected_range",
                schema: "decision_vault",
                table: "decisions",
                sql: "\"ExpectedSuccessScore\" BETWEEN 1 AND 100");

            migrationBuilder.AddCheckConstraint(
                name: "CK_decision_reviews_rating_range",
                schema: "decision_vault",
                table: "decision_reviews",
                sql: "\"outcome_rating\" BETWEEN 1 AND 5");

            migrationBuilder.AddCheckConstraint(
                name: "CK_decision_options_score_range",
                schema: "decision_vault",
                table: "decision_options",
                sql: "\"Score\" BETWEEN 0 AND 10");

            migrationBuilder.AddCheckConstraint(
                name: "CK_decision_options_weight_range",
                schema: "decision_vault",
                table: "decision_options",
                sql: "\"Weight\" BETWEEN 0 AND 10");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_decisions_confidence_range",
                schema: "decision_vault",
                table: "decisions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_decisions_expected_range",
                schema: "decision_vault",
                table: "decisions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_decision_reviews_rating_range",
                schema: "decision_vault",
                table: "decision_reviews");

            migrationBuilder.DropCheckConstraint(
                name: "CK_decision_options_score_range",
                schema: "decision_vault",
                table: "decision_options");

            migrationBuilder.DropCheckConstraint(
                name: "CK_decision_options_weight_range",
                schema: "decision_vault",
                table: "decision_options");
        }
    }
}
