using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowOps.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_type",
                table: "notifications");

            migrationBuilder.CreateTable(
                name: "role_changes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    previous_roles = table.Column<string[]>(type: "text[]", nullable: false),
                    proposed_roles = table.Column<string[]>(type: "text[]", nullable: false),
                    proposed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    decided_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_changes", x => x.id);
                    table.CheckConstraint("ck_role_changes_decision_iff_closed", "(status = 'Pending') = (decided_at IS NULL)");
                    table.CheckConstraint("ck_role_changes_reason_iff_rejected", "(status = 'Rejected') = (rejection_reason IS NOT NULL)");
                    table.CheckConstraint("ck_role_changes_status", "status IN ('Pending', 'Approved', 'Rejected', 'Cancelled', 'Expired')");
                    table.ForeignKey(
                        name: "fk_role_changes_users_decided_by_id",
                        column: x => x.decided_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_role_changes_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_type",
                table: "notifications",
                sql: "type IN ('TaskAssigned', 'RequestApproved', 'RequestRejected', 'RequestCompleted', 'StepApproved', 'CommentAdded', 'RoleChangePending', 'RoleChangeApproved', 'RoleChangeRejected')");

            migrationBuilder.CreateIndex(
                name: "ix_role_changes_decided_by_id",
                table: "role_changes",
                column: "decided_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_role_changes_proposed_at",
                table: "role_changes",
                column: "proposed_at",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_role_changes_user_id",
                table: "role_changes",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_role_changes_pending_per_user",
                table: "role_changes",
                column: "user_id",
                unique: true,
                filter: "status = 'Pending'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "role_changes");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_type",
                table: "notifications");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_type",
                table: "notifications",
                sql: "type IN ('TaskAssigned', 'RequestApproved', 'RequestRejected', 'RequestCompleted', 'StepApproved', 'CommentAdded')");
        }
    }
}
