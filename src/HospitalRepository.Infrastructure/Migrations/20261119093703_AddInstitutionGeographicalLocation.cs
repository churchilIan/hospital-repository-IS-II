using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HospitalRepository.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInstitutionGeographicalLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "County",
                table: "institutions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "institutions",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "institutions",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubCounty",
                table: "institutions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "County",
                table: "institutions");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "institutions");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "institutions");

            migrationBuilder.DropColumn(
                name: "SubCounty",
                table: "institutions");
        }
    }
}
