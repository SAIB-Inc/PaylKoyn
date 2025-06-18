using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaylKoyn.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIndex_OutputsBySlot_SpentSlot_Slot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_OutputsBySlot_SpentSlot_Slot",
                schema: "public",
                table: "OutputsBySlot",
                columns: new[] { "SpentSlot", "Slot" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutputsBySlot_SpentSlot_Slot",
                schema: "public",
                table: "OutputsBySlot");
        }
    }
}
