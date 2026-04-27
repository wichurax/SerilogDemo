using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SerilogDemo.Data;

#nullable disable

namespace SerilogDemo.Migrations
{
    [DbContext(typeof(EcommerceDbContext))]
    [Migration("20260428153000_AddPaymentStatusUpdatedAtUtc")]
    public class AddPaymentStatusUpdatedAtUtc : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PaymentStatusUpdatedAtUtc",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentStatusUpdatedAtUtc",
                table: "Orders");
        }
    }
}