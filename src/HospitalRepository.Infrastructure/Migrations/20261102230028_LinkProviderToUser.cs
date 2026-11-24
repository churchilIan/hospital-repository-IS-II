using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HospitalRepository.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LinkProviderToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "providers",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_providers_UserId",
                table: "providers",
                column: "UserId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_providers_AspNetUsers_UserId",
                table: "providers",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_providers_AspNetUsers_UserId",
                table: "providers");

            migrationBuilder.DropIndex(
                name: "IX_providers_UserId",
                table: "providers");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "providers");
        }
    }
}
