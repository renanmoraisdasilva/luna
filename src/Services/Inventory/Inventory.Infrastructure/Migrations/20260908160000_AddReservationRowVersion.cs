using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Luna.Inventory.Migrations;

public partial class AddReservationRowVersion : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<byte[]>(
            name: "RowVersion",
            table: "InventoryReservations",
            type: "rowversion",
            rowVersion: true,
            nullable: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "RowVersion",
            table: "InventoryReservations");
    }
}
