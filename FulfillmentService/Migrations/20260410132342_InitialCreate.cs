using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FulfillmentService.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "fulfillment_service");

            migrationBuilder.CreateTable(
                name: "FulfillmentAttempts",
                schema: "fulfillment_service",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    UserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Warehouse = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LastProcessedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FulfillmentAttempts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FulfillmentAttempts_MessageId",
                schema: "fulfillment_service",
                table: "FulfillmentAttempts",
                column: "MessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FulfillmentAttempts_OrderId",
                schema: "fulfillment_service",
                table: "FulfillmentAttempts",
                column: "OrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FulfillmentAttempts",
                schema: "fulfillment_service");
        }
    }
}
