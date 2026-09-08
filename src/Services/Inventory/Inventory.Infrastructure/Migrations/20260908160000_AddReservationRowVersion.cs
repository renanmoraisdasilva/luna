using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Luna.Inventory.Migrations;

/// <inheritdoc />
public partial class AddReservationRowVersion : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<byte[]>(
            name: "RowVersion",
            table: "InventoryReservations",
            type: "rowversion",
            rowVersion: true,
            nullable: false);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "RowVersion",
            table: "InventoryReservations");
    }
}
