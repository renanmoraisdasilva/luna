using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Luna.Orders.Infrastructure.Migrations
{
    public partial class AddShipmentReference : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ShipmentId",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShipmentId",
                table: "Orders");
        }
    }
}
