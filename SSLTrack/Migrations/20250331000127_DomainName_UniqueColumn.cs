using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SSLTrack.Migrations
{
    /// <inheritdoc />
    public partial class DomainName_UniqueColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Domains_DomainName",
                table: "Domains");

            migrationBuilder.CreateIndex(
                name: "IX_Domains_DomainName",
                table: "Domains",
                column: "DomainName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Domains_DomainName",
                table: "Domains");

            migrationBuilder.CreateIndex(
                name: "IX_Domains_DomainName",
                table: "Domains",
                column: "DomainName");
        }
    }
}
