using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowOps.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestsTasksActivitiesNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "request_number_seq");

            migrationBuilder.CreateTable(
                name: "requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    requester_id = table.Column<Guid>(type: "uuid", nullable: false),
                    department_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    stage = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_requests", x => x.id);
                    table.CheckConstraint("ck_requests_stage", "stage IN ('ManagerApproval', 'FinanceApproval', 'HrConfirmation', 'ProcurementFulfillment', 'Closed')");
                    table.CheckConstraint("ck_requests_status", "status IN ('InReview', 'Approved', 'Completed', 'Rejected')");
                    table.CheckConstraint("ck_requests_type", "type IN ('EquipmentPurchase', 'Leave')");
                    table.ForeignKey(
                        name: "fk_requests_departments_department_id",
                        column: x => x.department_id,
                        principalTable: "departments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_requests_users_requester_id",
                        column: x => x.requester_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "leave_request_details",
                columns: table => new
                {
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    leave_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_leave_request_details", x => x.request_id);
                    table.CheckConstraint("ck_leave_request_details_dates", "end_date >= start_date");
                    table.CheckConstraint("ck_leave_request_details_leave_type", "leave_type IN ('Annual', 'Sick', 'Unpaid', 'Other')");
                    table.ForeignKey(
                        name: "fk_leave_request_details_requests_id",
                        column: x => x.request_id,
                        principalTable: "requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchase_request_details",
                columns: table => new
                {
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    estimated_cost_amount = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    estimated_cost_currency = table.Column<string>(type: "char(3)", nullable: false),
                    justification = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_purchase_request_details", x => x.request_id);
                    table.CheckConstraint("ck_purchase_request_details_amount", "estimated_cost_amount > 0 AND estimated_cost_amount <= 1000000.000");
                    table.CheckConstraint("ck_purchase_request_details_currency", "estimated_cost_currency IN ('JOD')");
                    table.ForeignKey(
                        name: "fk_purchase_request_details_requests_id",
                        column: x => x.request_id,
                        principalTable: "requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "work_tasks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    stage = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    assignee_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assignee_role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    decided_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_tasks", x => x.id);
                    table.CheckConstraint("ck_work_tasks_assignee_role", "assignee_role IN ('Administrator', 'Employee', 'FinanceOfficer', 'HrOfficer', 'ProcurementOfficer')");
                    table.CheckConstraint("ck_work_tasks_decision_iff_done", "(status = 'Open') = (outcome IS NULL AND decided_by_id IS NULL AND decided_at IS NULL)");
                    table.CheckConstraint("ck_work_tasks_kind", "kind IN ('Approval', 'Fulfillment')");
                    table.CheckConstraint("ck_work_tasks_one_assignee", "num_nonnulls(assignee_user_id, assignee_role) = 1");
                    table.CheckConstraint("ck_work_tasks_outcome", "outcome IN ('Approved', 'Rejected', 'Fulfilled')");
                    table.CheckConstraint("ck_work_tasks_stage", "stage IN ('ManagerApproval', 'FinanceApproval', 'HrConfirmation', 'ProcurementFulfillment', 'Closed')");
                    table.CheckConstraint("ck_work_tasks_status", "status IN ('Open', 'Done')");
                    table.ForeignKey(
                        name: "fk_work_tasks_requests_request_id",
                        column: x => x.request_id,
                        principalTable: "requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_work_tasks_users_assignee_user_id",
                        column: x => x.assignee_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_work_tasks_users_decided_by_id",
                        column: x => x.decided_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    body = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    task_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications", x => x.id);
                    table.CheckConstraint("ck_notifications_type", "type IN ('TaskAssigned', 'RequestApproved', 'RequestRejected', 'RequestCompleted', 'StepApproved', 'CommentAdded')");
                    table.ForeignKey(
                        name: "fk_notifications_requests_request_id",
                        column: x => x.request_id,
                        principalTable: "requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_notifications_users_recipient_id",
                        column: x => x.recipient_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_notifications_work_task_task_id",
                        column: x => x.task_id,
                        principalTable: "work_tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "request_activities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    task_id = table.Column<Guid>(type: "uuid", nullable: true),
                    stage = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_request_activities", x => x.id);
                    table.CheckConstraint("ck_request_activities_stage", "stage IN ('ManagerApproval', 'FinanceApproval', 'HrConfirmation', 'ProcurementFulfillment', 'Closed')");
                    table.CheckConstraint("ck_request_activities_type", "type IN ('Submitted', 'TaskAssigned', 'Approved', 'Rejected', 'Fulfilled', 'Completed', 'CommentAdded')");
                    table.ForeignKey(
                        name: "fk_request_activities_requests_request_id",
                        column: x => x.request_id,
                        principalTable: "requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_request_activities_users_actor_id",
                        column: x => x.actor_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_request_activities_work_task_task_id",
                        column: x => x.task_id,
                        principalTable: "work_tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_notifications_recipient_id_created_at",
                table: "notifications",
                columns: new[] { "recipient_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_notifications_request_id",
                table: "notifications",
                column: "request_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_task_id",
                table: "notifications",
                column: "task_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_unread_recipient_id",
                table: "notifications",
                column: "recipient_id",
                filter: "read_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_request_activities_actor_id",
                table: "request_activities",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "ix_request_activities_request_id_occurred_at",
                table: "request_activities",
                columns: new[] { "request_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_request_activities_task_id",
                table: "request_activities",
                column: "task_id");

            migrationBuilder.CreateIndex(
                name: "ix_requests_department_id",
                table: "requests",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "ix_requests_requester_id_submitted_at",
                table: "requests",
                columns: new[] { "requester_id", "submitted_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_requests_status",
                table: "requests",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ux_requests_number",
                table: "requests",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_work_tasks_decided_by_id",
                table: "work_tasks",
                column: "decided_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_tasks_open_assignee_role",
                table: "work_tasks",
                column: "assignee_role",
                filter: "status = 'Open'");

            migrationBuilder.CreateIndex(
                name: "ix_work_tasks_open_assignee_user_id",
                table: "work_tasks",
                column: "assignee_user_id",
                filter: "status = 'Open'");

            migrationBuilder.CreateIndex(
                name: "ix_work_tasks_request_id",
                table: "work_tasks",
                column: "request_id");

            migrationBuilder.CreateIndex(
                name: "ux_work_tasks_open_per_request",
                table: "work_tasks",
                column: "request_id",
                unique: true,
                filter: "status = 'Open'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "leave_request_details");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "purchase_request_details");

            migrationBuilder.DropTable(
                name: "request_activities");

            migrationBuilder.DropTable(
                name: "work_tasks");

            migrationBuilder.DropTable(
                name: "requests");

            migrationBuilder.DropSequence(
                name: "request_number_seq");
        }
    }
}
