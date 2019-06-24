using Microsoft.EntityFrameworkCore.Migrations;

namespace APIViewWebApp.Migrations
{
    public partial class DllPath : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DllPath",
                table: "DLL",
                nullable: false,
                defaultValue: "");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DllPath",
                table: "DLL");
        }
    }
}
