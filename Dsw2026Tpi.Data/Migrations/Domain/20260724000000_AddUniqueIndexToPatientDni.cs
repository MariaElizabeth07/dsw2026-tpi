using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dsw2026Tpi.Data.Migrations.Domain
{
    /// <inheritdoc />
    public partial class AddUniqueIndexToPatientDni : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Patients_Dni",
                table: "Patients");

            migrationBuilder.CreateIndex(
                name: "IX_Patients_Dni",
                table: "Patients",
                column: "Dni",
                unique: true,
                filter: "[Deleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Patients_Dni",
                table: "Patients");

            migrationBuilder.CreateIndex(
                name: "IX_Patients_Dni",
                table: "Patients",
                column: "Dni");
        }
    }
}
