using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VelvetChess.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPuzzleResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PuzzleResults",
                columns: table => new
                {
                    PlayerId = table.Column<string>(type: "TEXT", nullable: false),
                    PuzzleId = table.Column<string>(type: "TEXT", nullable: false),
                    Attempts = table.Column<int>(type: "INTEGER", nullable: false),
                    SolvedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PuzzleResults", x => new { x.PlayerId, x.PuzzleId });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PuzzleResults");
        }
    }
}
