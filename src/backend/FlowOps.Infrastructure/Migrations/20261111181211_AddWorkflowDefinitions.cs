using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowOps.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowDefinitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Nullable until the backfill below has given every existing request its v1.
            migrationBuilder.AddColumn<Guid>(
                name: "workflow_version_id",
                table: "requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "workflow_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workflow_definitions", x => x.id);
                    table.CheckConstraint("ck_workflow_definitions_request_type", "request_type IN ('EquipmentPurchase', 'Leave')");
                });

            migrationBuilder.CreateTable(
                name: "workflow_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workflow_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    published_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    retired_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workflow_versions", x => x.id);
                    table.CheckConstraint("ck_workflow_versions_number", "number >= 1");
                    table.CheckConstraint("ck_workflow_versions_published_iff_not_draft", "(status = 'Draft') = (published_at IS NULL)");
                    table.CheckConstraint("ck_workflow_versions_retired_iff_retired", "(status = 'Retired') = (retired_at IS NOT NULL)");
                    table.CheckConstraint("ck_workflow_versions_status", "status IN ('Draft', 'Published', 'Retired')");
                    table.ForeignKey(
                        name: "fk_workflow_versions_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_workflow_versions_users_published_by_id",
                        column: x => x.published_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_workflow_versions_workflow_definitions_workflow_id",
                        column: x => x.workflow_id,
                        principalTable: "workflow_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "workflow_steps",
                columns: table => new
                {
                    order = table.Column<int>(type: "integer", nullable: false),
                    workflow_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    assignee_kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    assignee_role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workflow_steps", x => new { x.workflow_version_id, x.order });
                    table.CheckConstraint("ck_workflow_steps_assignee_kind", "assignee_kind IN ('RequesterManager', 'Role', 'DepartmentRole', 'SpecificUser', 'ManagerOfManager')");
                    table.CheckConstraint("ck_workflow_steps_assignee_role", "(assignee_kind = 'Role') = (assignee_role IS NOT NULL) AND assignee_role <> 'Administrator'");
                    table.CheckConstraint("ck_workflow_steps_kind", "kind IN ('Approval', 'Fulfillment')");
                    table.CheckConstraint("ck_workflow_steps_order", "\"order\" >= 0");
                    table.ForeignKey(
                        name: "fk_workflow_steps_workflow_versions_workflow_version_id",
                        column: x => x.workflow_version_id,
                        principalTable: "workflow_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            SeedVersionOne(migrationBuilder);

            // Every existing request, in any state, ran on the steps v1 now holds (DOMAIN_MODEL.md §7.4).
            migrationBuilder.Sql($"""
                UPDATE requests SET workflow_version_id = CASE type
                    WHEN 'EquipmentPurchase' THEN '{PurchaseV1}'::uuid
                    WHEN 'Leave' THEN '{LeaveV1}'::uuid
                END;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "workflow_version_id",
                table: "requests",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_requests_workflow_version_id",
                table: "requests",
                column: "workflow_version_id");

            migrationBuilder.CreateIndex(
                name: "ux_workflow_definitions_request_type",
                table: "workflow_definitions",
                column: "request_type",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_workflow_steps_key",
                table: "workflow_steps",
                columns: new[] { "workflow_version_id", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_workflow_versions_created_by_id",
                table: "workflow_versions",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_versions_published_by_id",
                table: "workflow_versions",
                column: "published_by_id");

            migrationBuilder.CreateIndex(
                name: "ux_workflow_versions_draft",
                table: "workflow_versions",
                column: "workflow_id",
                unique: true,
                filter: "status = 'Draft'");

            migrationBuilder.CreateIndex(
                name: "ux_workflow_versions_number",
                table: "workflow_versions",
                columns: new[] { "workflow_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_workflow_versions_published",
                table: "workflow_versions",
                column: "workflow_id",
                unique: true,
                filter: "status = 'Published'");

            migrationBuilder.AddForeignKey(
                name: "fk_requests_workflow_versions_workflow_version_id",
                table: "requests",
                column: "workflow_version_id",
                principalTable: "workflow_versions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        // Fixed ids, so the seed is the same in every database. Frozen here: later migrations must not depend on code.
        private const string PurchaseWorkflow = "01937c00-0000-7000-8000-000000000001";
        private const string LeaveWorkflow = "01937c00-0000-7000-8000-000000000002";
        private const string PurchaseV1 = "01937c00-0000-7000-8000-000000000011";
        private const string LeaveV1 = "01937c00-0000-7000-8000-000000000012";

        /// <summary>
        /// Purchase and leave as Published v1, step for step what Phase 1's <c>FixedProcesses</c> held (P2-BE-03), with the
        /// v1 keys and names of DOMAIN_MODEL.md §7.4. No user created them, so <c>created_by_id</c>/<c>published_by_id</c> are null.
        /// </summary>
        private static void SeedVersionOne(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql($"""
                INSERT INTO workflow_definitions (id, request_type, name) VALUES
                    ('{PurchaseWorkflow}', 'EquipmentPurchase', 'Equipment purchase'),
                    ('{LeaveWorkflow}', 'Leave', 'Leave');

                INSERT INTO workflow_versions (id, workflow_id, number, status, created_at, created_by_id, published_at, published_by_id, retired_at) VALUES
                    ('{PurchaseV1}', '{PurchaseWorkflow}', 1, 'Published', '2026-11-11T00:00:00Z', NULL, '2026-11-11T00:00:00Z', NULL, NULL),
                    ('{LeaveV1}', '{LeaveWorkflow}', 1, 'Published', '2026-11-11T00:00:00Z', NULL, '2026-11-11T00:00:00Z', NULL, NULL);

                INSERT INTO workflow_steps (workflow_version_id, "order", key, name, kind, assignee_kind, assignee_role) VALUES
                    ('{PurchaseV1}', 0, 'manager-approval', 'Manager approval', 'Approval', 'RequesterManager', NULL),
                    ('{PurchaseV1}', 1, 'finance-approval', 'Finance approval', 'Approval', 'Role', 'FinanceOfficer'),
                    ('{PurchaseV1}', 2, 'procurement-fulfillment', 'Procurement fulfilment', 'Fulfillment', 'Role', 'ProcurementOfficer'),
                    ('{LeaveV1}', 0, 'manager-approval', 'Manager approval', 'Approval', 'RequesterManager', NULL),
                    ('{LeaveV1}', 1, 'hr-confirmation', 'HR confirmation', 'Approval', 'Role', 'HrOfficer');
                """);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_requests_workflow_versions_workflow_version_id",
                table: "requests");

            migrationBuilder.DropTable(
                name: "workflow_steps");

            migrationBuilder.DropTable(
                name: "workflow_versions");

            migrationBuilder.DropTable(
                name: "workflow_definitions");

            migrationBuilder.DropIndex(
                name: "ix_requests_workflow_version_id",
                table: "requests");

            migrationBuilder.DropColumn(
                name: "workflow_version_id",
                table: "requests");
        }
    }
}
