using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FulfillmentService.Migrations
{
    /// <inheritdoc />
    public partial class AddFulfillmentWorkflowAndOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CollectedAtUtc",
                schema: "fulfillment_service",
                table: "FulfillmentAttempts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                schema: "fulfillment_service",
                table: "FulfillmentAttempts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "DeliveryCourier",
                schema: "fulfillment_service",
                table: "FulfillmentAttempts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DeliveryOptionName",
                schema: "fulfillment_service",
                table: "FulfillmentAttempts",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "PackedAtUtc",
                schema: "fulfillment_service",
                table: "FulfillmentAttempts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ShippedAtUtc",
                schema: "fulfillment_service",
                table: "FulfillmentAttempts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrackingReference",
                schema: "fulfillment_service",
                table: "FulfillmentAttempts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FulfillmentAttemptItems",
                schema: "fulfillment_service",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FulfillmentAttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FulfillmentAttemptItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FulfillmentAttemptItems_FulfillmentAttempts_FulfillmentAtte~",
                        column: x => x.FulfillmentAttemptId,
                        principalSchema: "fulfillment_service",
                        principalTable: "FulfillmentAttempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "fulfillment_service",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RoutingKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    TraceParent = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TraceState = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FulfillmentAttemptItems_FulfillmentAttemptId",
                schema: "fulfillment_service",
                table: "FulfillmentAttemptItems",
                column: "FulfillmentAttemptId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_OccurredAtUtc",
                schema: "fulfillment_service",
                table: "OutboxMessages",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_PublishedAtUtc",
                schema: "fulfillment_service",
                table: "OutboxMessages",
                column: "PublishedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FulfillmentAttemptItems",
                schema: "fulfillment_service");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "fulfillment_service");

            migrationBuilder.DropColumn(
                name: "CollectedAtUtc",
                schema: "fulfillment_service",
                table: "FulfillmentAttempts");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                schema: "fulfillment_service",
                table: "FulfillmentAttempts");

            migrationBuilder.DropColumn(
                name: "DeliveryCourier",
                schema: "fulfillment_service",
                table: "FulfillmentAttempts");

            migrationBuilder.DropColumn(
                name: "DeliveryOptionName",
                schema: "fulfillment_service",
                table: "FulfillmentAttempts");

            migrationBuilder.DropColumn(
                name: "PackedAtUtc",
                schema: "fulfillment_service",
                table: "FulfillmentAttempts");

            migrationBuilder.DropColumn(
                name: "ShippedAtUtc",
                schema: "fulfillment_service",
                table: "FulfillmentAttempts");

            migrationBuilder.DropColumn(
                name: "TrackingReference",
                schema: "fulfillment_service",
                table: "FulfillmentAttempts");
        }
    }
}
