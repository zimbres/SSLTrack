#nullable disable
namespace SSLTrack.Migrations
{
    /// <inheritdoc/>
    public partial class AuthenticationAndSmtp : Migration
    {
        /// <inheritdoc/>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(name: "ServiceAccounts", columns: table => new { Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true), Email = table.Column<string>(type: "TEXT", nullable: false), NormalizedEmail = table.Column<string>(type: "TEXT", nullable: false), PasswordHash = table.Column<string>(type: "TEXT", nullable: false), AgentId = table.Column<int>(type: "INTEGER", nullable: false), Enabled = table.Column<bool>(type: "INTEGER", nullable: false), FailedAttempts = table.Column<int>(type: "INTEGER", nullable: false), LockoutUntil = table.Column<DateTime>(type: "TEXT", nullable: true) }, constraints: table =>
            {
                table.PrimaryKey("PK_ServiceAccounts", x => x.Id);
                table.ForeignKey(name: "FK_ServiceAccounts_Agents_AgentId", column: x => x.AgentId, principalTable: "Agents", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            });
            migrationBuilder.CreateTable(name: "SmtpSettings", columns: table => new { Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true), Name = table.Column<string>(type: "TEXT", nullable: false), MailFrom = table.Column<string>(type: "TEXT", nullable: false), MailTo = table.Column<string>(type: "TEXT", nullable: false), Bcc = table.Column<string>(type: "TEXT", nullable: true), SmtpHost = table.Column<string>(type: "TEXT", nullable: false), Port = table.Column<int>(type: "INTEGER", nullable: false), Username = table.Column<string>(type: "TEXT", nullable: true), ProtectedPassword = table.Column<string>(type: "TEXT", nullable: true), TlsMode = table.Column<int>(type: "INTEGER", nullable: false) }, constraints: table =>
            {
                table.PrimaryKey("PK_SmtpSettings", x => x.Id);
            });
            migrationBuilder.CreateTable(name: "Users", columns: table => new { Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true), Email = table.Column<string>(type: "TEXT", nullable: false), NormalizedEmail = table.Column<string>(type: "TEXT", nullable: false), DisplayName = table.Column<string>(type: "TEXT", nullable: false), Role = table.Column<string>(type: "TEXT", nullable: false), AuthenticationType = table.Column<string>(type: "TEXT", nullable: false), Enabled = table.Column<bool>(type: "INTEGER", nullable: false), RequirePasswordChange = table.Column<bool>(type: "INTEGER", nullable: false), PasswordHash = table.Column<string>(type: "TEXT", nullable: true), SecurityStamp = table.Column<string>(type: "TEXT", nullable: false), OidcIssuer = table.Column<string>(type: "TEXT", nullable: true), OidcSubject = table.Column<string>(type: "TEXT", nullable: true), FailedAttempts = table.Column<int>(type: "INTEGER", nullable: false), LockoutUntil = table.Column<DateTime>(type: "TEXT", nullable: true), AvatarType = table.Column<string>(type: "TEXT", nullable: false), AvatarUrl = table.Column<string>(type: "TEXT", nullable: true) }, constraints: table =>
            {
                table.PrimaryKey("PK_Users", x => x.Id);
            });
            migrationBuilder.CreateIndex(name: "IX_ServiceAccounts_AgentId", table: "ServiceAccounts", column: "AgentId");
            migrationBuilder.CreateIndex(name: "IX_ServiceAccounts_NormalizedEmail", table: "ServiceAccounts", column: "NormalizedEmail", unique: true);
            migrationBuilder.CreateIndex(name: "IX_Users_NormalizedEmail", table: "Users", column: "NormalizedEmail", unique: true);
            migrationBuilder.CreateIndex(name: "IX_Users_OidcIssuer_OidcSubject", table: "Users", columns: new[] { "OidcIssuer", "OidcSubject" }, unique: true);
        }

        /// <inheritdoc/>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ServiceAccounts");
            migrationBuilder.DropTable(name: "SmtpSettings");
            migrationBuilder.DropTable(name: "Users");
        }
    }
}
