using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FlowOps.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AuthAndOrganization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "created_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "department_id",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "display_name",
                table: "users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<Guid>(
                name: "manager_id",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "must_change_password",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "departments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_departments", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "roles",
                columns: new[] { "id", "concurrency_stamp", "name", "normalized_name" },
                values: new object[,]
                {
                    { new Guid("0193a000-0000-7000-8000-000000000001"), "0193a000-0000-7000-8000-000000000001", "Administrator", "ADMINISTRATOR" },
                    { new Guid("0193a000-0000-7000-8000-000000000002"), "0193a000-0000-7000-8000-000000000002", "Employee", "EMPLOYEE" },
                    { new Guid("0193a000-0000-7000-8000-000000000003"), "0193a000-0000-7000-8000-000000000003", "FinanceOfficer", "FINANCEOFFICER" },
                    { new Guid("0193a000-0000-7000-8000-000000000004"), "0193a000-0000-7000-8000-000000000004", "HrOfficer", "HROFFICER" },
                    { new Guid("0193a000-0000-7000-8000-000000000005"), "0193a000-0000-7000-8000-000000000005", "ProcurementOfficer", "PROCUREMENTOFFICER" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_users_department_id",
                table: "users",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_manager_id",
                table: "users",
                column: "manager_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_manager_not_self",
                table: "users",
                sql: "manager_id <> id");

            migrationBuilder.CreateIndex(
                name: "ux_departments_code",
                table: "departments",
                column: "code",
                unique: true);

            // Department names are unique case-insensitively (DOMAIN_MODEL.md §2.1); EF has no model for functional indexes.
            migrationBuilder.Sql("CREATE UNIQUE INDEX ux_departments_name_ci ON departments (lower(name));");

            migrationBuilder.AddForeignKey(
                name: "fk_users_departments_department_id",
                table: "users",
                column: "department_id",
                principalTable: "departments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_users_users_manager_id",
                table: "users",
                column: "manager_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ux_departments_name_ci;");

            migrationBuilder.DropForeignKey(
                name: "fk_users_departments_department_id",
                table: "users");

            migrationBuilder.DropForeignKey(
                name: "fk_users_users_manager_id",
                table: "users");

            migrationBuilder.DropTable(
                name: "departments");

            migrationBuilder.DropIndex(
                name: "ix_users_department_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_users_manager_id",
                table: "users");

            migrationBuilder.DropCheckConstraint(
                name: "ck_users_manager_not_self",
                table: "users");

            migrationBuilder.DeleteData(
                table: "roles",
                keyColumn: "id",
                keyValue: new Guid("0193a000-0000-7000-8000-000000000001"));

            migrationBuilder.DeleteData(
                table: "roles",
                keyColumn: "id",
                keyValue: new Guid("0193a000-0000-7000-8000-000000000002"));

            migrationBuilder.DeleteData(
                table: "roles",
                keyColumn: "id",
                keyValue: new Guid("0193a000-0000-7000-8000-000000000003"));

            migrationBuilder.DeleteData(
                table: "roles",
                keyColumn: "id",
                keyValue: new Guid("0193a000-0000-7000-8000-000000000004"));

            migrationBuilder.DeleteData(
                table: "roles",
                keyColumn: "id",
                keyValue: new Guid("0193a000-0000-7000-8000-000000000005"));

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "users");

            migrationBuilder.DropColumn(
                name: "department_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "display_name",
                table: "users");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "users");

            migrationBuilder.DropColumn(
                name: "manager_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "must_change_password",
                table: "users");
        }
    }
}
