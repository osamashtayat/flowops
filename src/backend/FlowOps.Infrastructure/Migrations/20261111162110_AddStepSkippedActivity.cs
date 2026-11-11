using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowOps.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStepSkippedActivity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_request_activities_type",
                table: "request_activities");

            migrationBuilder.AddCheckConstraint(
                name: "ck_request_activities_type",
                table: "request_activities",
                sql: "type IN ('Submitted', 'TaskAssigned', 'Approved', 'Rejected', 'Fulfilled', 'Completed', 'CommentAdded', 'StepSkipped')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_request_activities_type",
                table: "request_activities");

            migrationBuilder.AddCheckConstraint(
                name: "ck_request_activities_type",
                table: "request_activities",
                sql: "type IN ('Submitted', 'TaskAssigned', 'Approved', 'Rejected', 'Fulfilled', 'Completed', 'CommentAdded')");
        }
    }
}
