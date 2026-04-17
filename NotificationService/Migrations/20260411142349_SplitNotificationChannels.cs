using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NotificationService.Migrations
{
    /// <inheritdoc />
    public partial class SplitNotificationChannels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationAttempts",
                schema: "notification_service");

            migrationBuilder.CreateTable(
                name: "EmailNotificationDeliveries",
                schema: "notification_service",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    UserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RecipientEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ProcessedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailNotificationDeliveries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationUsers",
                schema: "notification_service",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    EmailEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    SmsEnabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationUsers", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "SmsNotificationDeliveries",
                schema: "notification_service",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    UserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RecipientPhoneNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ProcessedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmsNotificationDeliveries", x => x.Id);
                });

            migrationBuilder.InsertData(
                schema: "notification_service",
                table: "NotificationUsers",
                columns: new[] { "UserId", "Email", "EmailEnabled", "PhoneNumber", "SmsEnabled" },
                values: new object[,]
                {
                    { "demo-user-001", "demo.user.001@notifications.demo", true, "+15551000001", true },
                    { "demo-user-002", "demo.user.002@notifications.demo", true, "+15551000002", false },
                    { "demo-user-003", "demo.user.003@notifications.demo", false, "+15551000003", true },
                    { "demo-user-004", "demo.user.004@notifications.demo", true, "+15551000004", true },
                    { "demo-user-005", "demo.user.005@notifications.demo", false, "+15551000005", false },
                    { "demo-user-006", "demo.user.006@notifications.demo", true, "+15551000006", true },
                    { "demo-user-007", "demo.user.007@notifications.demo", true, "+15551000007", false },
                    { "demo-user-008", "demo.user.008@notifications.demo", false, "+15551000008", true },
                    { "demo-user-009", "demo.user.009@notifications.demo", true, "+15551000009", true },
                    { "demo-user-123", "demo.user.123@notifications.demo", true, "+15551000123", true }
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailNotificationDeliveries_MessageId",
                schema: "notification_service",
                table: "EmailNotificationDeliveries",
                column: "MessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailNotificationDeliveries_OrderId",
                schema: "notification_service",
                table: "EmailNotificationDeliveries",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_SmsNotificationDeliveries_MessageId",
                schema: "notification_service",
                table: "SmsNotificationDeliveries",
                column: "MessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SmsNotificationDeliveries_OrderId",
                schema: "notification_service",
                table: "SmsNotificationDeliveries",
                column: "OrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailNotificationDeliveries",
                schema: "notification_service");

            migrationBuilder.DropTable(
                name: "NotificationUsers",
                schema: "notification_service");

            migrationBuilder.DropTable(
                name: "SmsNotificationDeliveries",
                schema: "notification_service");

            migrationBuilder.CreateTable(
                name: "NotificationAttempts",
                schema: "notification_service",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    Channel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    LastError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LastProcessedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MessageId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationAttempts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationAttempts_MessageId",
                schema: "notification_service",
                table: "NotificationAttempts",
                column: "MessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationAttempts_OrderId",
                schema: "notification_service",
                table: "NotificationAttempts",
                column: "OrderId");
        }
    }
}
