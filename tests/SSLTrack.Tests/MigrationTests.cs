namespace SSLTrack.Tests;

public class MigrationTests
{
    [Fact]
    public async Task Existing_domains_survive_authentication_migration()
    {
        var file = Path.Combine(Path.GetTempPath(), $"ssltrack-migrate-{Guid.NewGuid()}.db");
        try
        {
            using var db = new TestDbFactory(file).CreateDbContext();
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20250406144113_PublicPrefix");
            await db.Database.ExecuteSqlRawAsync("INSERT INTO Domains (DomainName, Port, Agent, CertCN, Issuer, ExpiryDate, LastChecked, UserId, Silenced, PublicPrefix) VALUES ('existing.example.com',443,0,'existing','issuer','2027-01-01','2026-10-27','User',0,0)");
            await migrator.MigrateAsync();
            Assert.Equal("existing.example.com", (await db.Domains.SingleAsync()).DomainName);
            Assert.Empty(await db.Users.ToListAsync());
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(file);
        }
    }
}
