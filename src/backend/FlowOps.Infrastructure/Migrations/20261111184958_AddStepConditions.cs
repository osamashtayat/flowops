using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowOps.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStepConditions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "condition_fact",
                table: "workflow_steps",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "condition_operator",
                table: "workflow_steps",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "condition_values",
                table: "workflow_steps",
                type: "text[]",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_workflow_steps_condition_complete",
                table: "workflow_steps",
                sql: "num_nonnulls(condition_fact, condition_operator, condition_values) IN (0, 3)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_workflow_steps_condition_fact",
                table: "workflow_steps",
                sql: "condition_fact IN ('Amount', 'LeaveType', 'Days')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_workflow_steps_condition_operator",
                table: "workflow_steps",
                sql: "condition_operator IN ('GreaterThan', 'GreaterThanOrEqual', 'LessThan', 'LessThanOrEqual', 'Equal', 'Is', 'IsOneOf', 'Before', 'After')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_workflow_steps_first_unconditional",
                table: "workflow_steps",
                sql: "\"order\" > 0 OR condition_fact IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_workflow_steps_condition_complete",
                table: "workflow_steps");

            migrationBuilder.DropCheckConstraint(
                name: "ck_workflow_steps_condition_fact",
                table: "workflow_steps");

            migrationBuilder.DropCheckConstraint(
                name: "ck_workflow_steps_condition_operator",
                table: "workflow_steps");

            migrationBuilder.DropCheckConstraint(
                name: "ck_workflow_steps_first_unconditional",
                table: "workflow_steps");

            migrationBuilder.DropColumn(
                name: "condition_fact",
                table: "workflow_steps");

            migrationBuilder.DropColumn(
                name: "condition_operator",
                table: "workflow_steps");

            migrationBuilder.DropColumn(
                name: "condition_values",
                table: "workflow_steps");
        }
    }
}
