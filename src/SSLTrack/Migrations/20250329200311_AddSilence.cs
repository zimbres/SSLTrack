using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SSLTrack.Migrations
{
    /// <inheritdoc />
    public partial class AddSilence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Silenced",
                table: "Domains",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Silenced",
                table: "Domains");
        }
    }
}
