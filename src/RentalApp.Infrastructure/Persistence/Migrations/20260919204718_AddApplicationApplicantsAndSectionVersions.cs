using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentalApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationApplicantsAndSectionVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ApplicantInfoVersion",
                table: "RentalApplications",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ResidenceHistoryVersion",
                table: "RentalApplications",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ApplicationApplicants",
                columns: table => new
                {
                    RentalApplicationId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    AddedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationApplicants", x => new { x.RentalApplicationId, x.UserId });
                    table.ForeignKey(
                        name: "FK_ApplicationApplicants_RentalApplications_RentalApplicationId",
                        column: x => x.RentalApplicationId,
                        principalTable: "RentalApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationApplicants_UserId",
                table: "ApplicationApplicants",
                column: "UserId");

            migrationBuilder.Sql("""
                INSERT INTO ApplicationApplicants (RentalApplicationId, UserId, AddedAtUtc)
                SELECT Id, ApplicantUserId, CreatedAtUtc
                FROM RentalApplications
                WHERE ApplicantUserId IS NOT NULL AND ApplicantUserId <> ''
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationApplicants");

            migrationBuilder.DropColumn(
                name: "ApplicantInfoVersion",
                table: "RentalApplications");

            migrationBuilder.DropColumn(
                name: "ResidenceHistoryVersion",
                table: "RentalApplications");
        }
    }
}
