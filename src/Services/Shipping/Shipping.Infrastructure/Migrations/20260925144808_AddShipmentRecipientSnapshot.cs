using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Luna.Shipping.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddShipmentRecipientSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RecipientAddressLine1",
                table: "Shipments",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RecipientAddressLine2",
                table: "Shipments",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecipientCity",
                table: "Shipments",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RecipientCountry",
                table: "Shipments",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "RecipientCustomerId",
                table: "Shipments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "RecipientFullName",
                table: "Shipments",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RecipientPostalCode",
                table: "Shipments",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RecipientStateOrProvince",
                table: "Shipments",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecipientAddressLine1",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "RecipientAddressLine2",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "RecipientCity",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "RecipientCountry",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "RecipientCustomerId",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "RecipientFullName",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "RecipientPostalCode",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "RecipientStateOrProvince",
                table: "Shipments");
        }
    }
}
