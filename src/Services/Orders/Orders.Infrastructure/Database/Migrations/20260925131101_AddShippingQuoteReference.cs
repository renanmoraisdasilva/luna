using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Luna.Orders.Infrastructure.Database.Migrations
{
    public partial class AddShippingQuoteReference : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ShippingQuoteId",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShippingQuoteId",
                table: "Orders");
        }
    }
}
