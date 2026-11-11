using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowOps.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StoreStepKeys : Migration
    {
        /// <summary>
        /// Phase 1 stage → seeded v1 step key and name, one to one (DOMAIN_MODEL.md §7.4). <c>Closed</c> has no row: it is a
        /// status, so a closed request and the <c>Completed</c> activity get no step. Frozen here, like the seed it matches.
        /// </summary>
        private const string StageToStep = """
            (VALUES ('ManagerApproval', 'manager-approval', 'Manager approval'),
                    ('FinanceApproval', 'finance-approval', 'Finance approval'),
                    ('HrConfirmation', 'hr-confirmation', 'HR confirmation'),
                    ('ProcurementFulfillment', 'procurement-fulfillment', 'Procurement fulfilment'))
                AS map(stage, key, name)
            """;

        private static readonly string[] Tables = ["requests", "work_tasks", "request_activities"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
            {
                migrationBuilder.AddColumn<string>(name: "step_key", table: table, type: "character varying(40)", maxLength: 40, nullable: true);
                migrationBuilder.AddColumn<string>(name: "step_name", table: table, type: "character varying(80)", maxLength: 80, nullable: true);
                // History keeps reading the same: every stored stage becomes the v1 key and the name the API already showed.
                migrationBuilder.Sql($"UPDATE {table} t SET step_key = map.key, step_name = map.name FROM {StageToStep} WHERE t.stage = map.stage;");
                migrationBuilder.DropCheckConstraint(name: $"ck_{table}_stage", table: table);
                migrationBuilder.DropColumn(name: "stage", table: table);
            }

            // Every task belongs to a step.
            migrationBuilder.AlterColumn<string>(name: "step_key", table: "work_tasks", type: "character varying(40)", maxLength: 40, nullable: false,
                oldClrType: typeof(string), oldType: "character varying(40)", oldMaxLength: 40, oldNullable: true);
            migrationBuilder.AlterColumn<string>(name: "step_name", table: "work_tasks", type: "character varying(80)", maxLength: 80, nullable: false,
                oldClrType: typeof(string), oldType: "character varying(80)", oldMaxLength: 80, oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_requests_step_iff_open",
                table: "requests",
                sql: "(status IN ('Completed', 'Rejected')) = (step_key IS NULL) AND (step_key IS NULL) = (step_name IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_request_activities_step",
                table: "request_activities",
                sql: "(step_key IS NULL) = (step_name IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(name: "ck_requests_step_iff_open", table: "requests");
            migrationBuilder.DropCheckConstraint(name: "ck_request_activities_step", table: "request_activities");

            foreach (var table in Tables)
            {
                migrationBuilder.AddColumn<string>(name: "stage", table: table, type: "character varying(32)", maxLength: 32, nullable: true);
                migrationBuilder.Sql($"UPDATE {table} t SET stage = map.stage FROM {StageToStep} WHERE t.step_key = map.key;");
                migrationBuilder.DropColumn(name: "step_key", table: table);
                migrationBuilder.DropColumn(name: "step_name", table: table);
            }

            // Phase 1 stored Closed on closed requests and on Completed activities.
            migrationBuilder.Sql("UPDATE requests SET stage = 'Closed' WHERE stage IS NULL;");
            migrationBuilder.Sql("UPDATE request_activities SET stage = 'Closed' WHERE type = 'Completed';");
            foreach (var table in new[] { "requests", "work_tasks" })
            {
                migrationBuilder.AlterColumn<string>(name: "stage", table: table, type: "character varying(32)", maxLength: 32, nullable: false,
                    oldClrType: typeof(string), oldType: "character varying(32)", oldMaxLength: 32, oldNullable: true);
            }

            foreach (var table in Tables)
            {
                migrationBuilder.AddCheckConstraint(
                    name: $"ck_{table}_stage",
                    table: table,
                    sql: "stage IN ('ManagerApproval', 'FinanceApproval', 'HrConfirmation', 'ProcurementFulfillment', 'Closed')");
            }
        }
    }
}
