using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orchestrator.Worker.Migrations
{
    /// <inheritdoc />
    public partial class SetDefaultCurrentState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "CurrentState",
                table: "Sagas",
                type: "text",
                nullable: false,
                defaultValue: "Initial",
                oldClrType: typeof(string),
                oldType: "text");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "CurrentState",
                table: "Sagas",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "Initial");
        }
    }
}
