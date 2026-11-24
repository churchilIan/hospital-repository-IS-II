using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HospitalRepository.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInstitutionToApplicationUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            //migrationBuilder.AddColumn<string>(
            //    name: "MasterPatientId",
            //    table: "patients",
            //    type: "character varying(100)",
            //    maxLength: 100,
            //    nullable: false,
            //    defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "InstitutionId",
                table: "AspNetUsers",
                type: "uuid",
                nullable: true);

            //migrationBuilder.CreateIndex(
            //    name: "IX_patients_MasterPatientId",
            //    table: "patients",
            //    column: "MasterPatientId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_InstitutionId",
                table: "AspNetUsers",
                column: "InstitutionId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_institutions_InstitutionId",
                table: "AspNetUsers",
                column: "InstitutionId",
                principalTable: "institutions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_institutions_InstitutionId",
                table: "AspNetUsers");

            //migrationBuilder.DropIndex(
            //    name: "IX_patients_MasterPatientId",
            //    table: "patients");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_InstitutionId",
                table: "AspNetUsers");

            //migrationBuilder.DropColumn(
            //    name: "MasterPatientId",
            //    table: "patients");

            migrationBuilder.DropColumn(
                name: "InstitutionId",
                table: "AspNetUsers");
        }
    }
}
