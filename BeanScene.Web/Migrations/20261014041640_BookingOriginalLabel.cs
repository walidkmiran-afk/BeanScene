using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeanScene.Web.Migrations
{
    /// <inheritdoc />
    public partial class BookingOriginalLabel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OriginalSittingType",
                table: "Reservations");

            migrationBuilder.AddColumn<string>(
                name: "OriginalLabel",
                table: "Reservations",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OriginalLabel",
                table: "Reservations");

            migrationBuilder.AddColumn<int>(
                name: "OriginalSittingType",
                table: "Reservations",
                type: "int",
                nullable: true);
        }
    }
}
