using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentalApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPropertyManagerNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PropertyManagerNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RentalApplicationId = table.Column<int>(type: "int", nullable: false),
                    AuthorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    AuthorDisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyManagerNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropertyManagerNotes_RentalApplications_RentalApplicationId",
                        column: x => x.RentalApplicationId,
                        principalTable: "RentalApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PropertyManagerNotes_RentalApplicationId_UpdatedAtUtc",
                table: "PropertyManagerNotes",
                columns: new[] { "RentalApplicationId", "UpdatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PropertyManagerNotes");
        }
    }
}

