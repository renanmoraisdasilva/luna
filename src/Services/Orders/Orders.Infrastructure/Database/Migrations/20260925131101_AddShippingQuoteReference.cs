using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Luna.Orders.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddShippingQuoteReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ShippingQuoteId",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShippingQuoteId",
                table: "Orders");
        }
    }
}
