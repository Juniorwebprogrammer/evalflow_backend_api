using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace evalflow_backend_api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Sign-in moves to Vexxen: users get their Vexxen account id (password, 2FA, email verification and reset columns
    /// go away) and companies keep their plan in step with the billing user's Vexxen subscription.
    /// Growth is renamed Professional, as in the Vexxen catalog.
    /// </summary>
    public partial class VexxenAuth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmailVerificado",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ExpiracionTokenRecuperacion",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TokenRecuperacionPassword",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TokenValidacionEmail",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TokenValidacionExpiracion",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TwoFactorCode",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TwoFactorCodeExpiration",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TwoFactorEnabled",
                table: "Users");

            migrationBuilder.AddColumn<string>(
                name: "VexxenUserId",
                table: "Users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingVexxenUserId",
                table: "Companies",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PlanAccessEndsAt",
                table: "Companies",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PlanManual",
                table: "Companies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PlanStatus",
                table: "Companies",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "none");

            migrationBuilder.AddColumn<DateTime>(
                name: "PlanSyncedAt",
                table: "Companies",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TrialEndsAt",
                table: "Companies",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Code", "Nombre" },
                values: new object[] { "professional", "Professional" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_VexxenUserId",
                table: "Users",
                column: "VexxenUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Companies_BillingVexxenUserId",
                table: "Companies",
                column: "BillingVexxenUserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_VexxenUserId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Companies_BillingVexxenUserId",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "VexxenUserId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "BillingVexxenUserId",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "PlanAccessEndsAt",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "PlanManual",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "PlanStatus",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "PlanSyncedAt",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "TrialEndsAt",
                table: "Companies");

            migrationBuilder.AddColumn<bool>(
                name: "EmailVerificado",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiracionTokenRecuperacion",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "Users",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TokenRecuperacionPassword",
                table: "Users",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TokenValidacionEmail",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TokenValidacionExpiracion",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TwoFactorCode",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TwoFactorCodeExpiration",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TwoFactorEnabled",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Code", "Nombre" },
                values: new object[] { "growth", "Growth" });
        }
    }
}
