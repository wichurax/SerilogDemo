using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SerilogDemo.Migrations
{
    /// <inheritdoc />
    public partial class AddWarehouseInventoryAndFulfillmentProjection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FulfillmentDispatchedAtUtc",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FulfillmentLastMessage",
                table: "Orders",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FulfillmentLastUpdatedAtUtc",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FulfillmentStatus",
                table: "Orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FulfillmentTrackingReference",
                table: "Orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FulfillmentWarehouse",
                table: "Orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WarehouseInventories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuantityOnHand = table.Column<int>(type: "integer", nullable: false),
                    QuantityReserved = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarehouseInventories", x => x.Id);
                    table.CheckConstraint("CK_WarehouseInventories_QuantityOnHand_NonNegative", "\"QuantityOnHand\" >= 0");
                    table.CheckConstraint("CK_WarehouseInventories_QuantityReserved_NonNegative", "\"QuantityReserved\" >= 0");
                    table.CheckConstraint("CK_WarehouseInventories_Reserved_NotGreaterThanOnHand", "\"QuantityReserved\" <= \"QuantityOnHand\"");
                    table.ForeignKey(
                        name: "FK_WarehouseInventories_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_MessageId",
                table: "InboxMessages",
                column: "MessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_ProcessedAtUtc",
                table: "InboxMessages",
                column: "ProcessedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseInventories_ItemId",
                table: "WarehouseInventories",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseInventories_WarehouseName_ItemId",
                table: "WarehouseInventories",
                columns: new[] { "WarehouseName", "ItemId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InboxMessages");

            migrationBuilder.DropTable(
                name: "WarehouseInventories");

            migrationBuilder.DropColumn(
                name: "FulfillmentDispatchedAtUtc",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "FulfillmentLastMessage",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "FulfillmentLastUpdatedAtUtc",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "FulfillmentStatus",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "FulfillmentTrackingReference",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "FulfillmentWarehouse",
                table: "Orders");
        }
    }
}
