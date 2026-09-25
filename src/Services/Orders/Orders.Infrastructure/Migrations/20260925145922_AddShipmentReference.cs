using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Luna.Orders.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddShipmentReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ShipmentId",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShipmentId",
                table: "Orders");
        }
    }
}
