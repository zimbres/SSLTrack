namespace SSLTrack.Tests;

public class AgentLifecycleTests
{
    [Fact]
    public async Task Agent_with_service_account_returns_useful_validation_instead_of_fk_failure()
    {
        var file = Path.Combine(Path.GetTempPath(), $"ssltrack-agent-delete-{Guid.NewGuid()}.db");
        try
        {
            var factory = new TestDbFactory(file);
            using (var db = factory.CreateDbContext())
            {
                await db.Database.MigrateAsync();
                db.Agents.Add(new Agent { Id = 42, Name = "Enrolled" });
                await db.SaveChangesAsync();
            }

            var accounts = new AccountService(factory);
            await accounts.SaveAsync(new UserEdit { Email = "admin@example.com", DisplayName = "Admin", Role = "Admin" }, "Long-Password-123!");
            var user = (await accounts.ValidatePasswordAsync("admin@example.com", "Long-Password-123!"))!;
            var provider = new ServiceCollection().BuildServiceProvider();
            var guard = new AccessGuard(provider, new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = AccountService.Principal(user) } }, accounts);
            var serviceAccounts = new ServiceAccountService(factory);
            await serviceAccounts.SaveAsync(new ServiceAccountEdit { Email = "agent@example.com", AgentId = 42 }, "Long-Password-123!");
            var services = new ServiceCollection().AddSingleton<IDbContextFactory<ApplicationDbContext>>(factory).AddSingleton<IAgentRepository>(new AgentRepository(factory)).AddSingleton(guard).BuildServiceProvider();
            var agents = ActivatorUtilities.CreateInstance<AgentService>(services);
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => agents.DeleteAgent("Enrolled"));
            Assert.Contains("service account", error.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(file);
        }
    }
}
