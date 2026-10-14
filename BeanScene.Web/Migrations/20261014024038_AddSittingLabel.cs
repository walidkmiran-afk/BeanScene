using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeanScene.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddSittingLabel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Label",
                table: "Sittings",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Label",
                table: "Sittings");
        }
    }
}
