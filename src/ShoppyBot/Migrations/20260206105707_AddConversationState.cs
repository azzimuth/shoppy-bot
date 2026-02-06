using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShoppyBot.Migrations
{
    /// <inheritdoc />
    public partial class AddConversationState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ConversationState",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PendingAction",
                table: "Users",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConversationState",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PendingAction",
                table: "Users");
        }
    }
}
