using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SerilogDemo.Migrations
{
    /// <inheritdoc />
    public partial class AddFulfillmentStageTimestamps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FulfillmentCollectedAtUtc",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FulfillmentPackedAtUtc",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FulfillmentCollectedAtUtc",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "FulfillmentPackedAtUtc",
                table: "Orders");
        }
    }
}
