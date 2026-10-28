namespace SSLTrack.Tests;

public class CircuitAccessTests
{
    [Fact]
    public async Task Stale_circuit_cannot_write_after_demotion_or_read_after_disable()
    {
        var file = Path.Combine(Path.GetTempPath(), $"ssltrack-circuit-{Guid.NewGuid()}.db");
        try
        {
            var factory = new TestDbFactory(file);
            using (var db = factory.CreateDbContext())
                await db.Database.MigrateAsync();
            var accounts = new AccountService(factory);
            await accounts.SaveAsync(new UserEdit { Email = "admin@example.com", DisplayName = "Admin", Role = "Admin" }, "Long-Password-123!");
            await accounts.SaveAsync(new UserEdit { Email = "other@example.com", DisplayName = "Other", Role = "Admin" }, "Long-Password-123!");
            var user = (await accounts.ValidatePasswordAsync("admin@example.com", "Long-Password-123!"))!;
            var services = new ServiceCollection().AddSingleton<AuthenticationStateProvider>(new FixedAuthState(AccountService.Principal(user))).BuildServiceProvider();
            var guard = new AccessGuard(services, new HttpContextAccessor(), accounts);
            Assert.Equal("Admin", (await guard.RequireAdminAsync()).Role);
            var edit = (await accounts.ListAsync()).Single(x => x.Id == user.Id);
            edit.Role = "ReadOnly";
            await accounts.SaveAsync(edit, null);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => guard.RequireAdminAsync());
            edit.Enabled = false;
            await accounts.SaveAsync(edit, null);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => guard.RequireHumanAsync());
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(file);
        }
    }

    [Fact]
    public async Task Expired_circuit_principal_cannot_continue_monitoring()
    {
        var file = Path.Combine(Path.GetTempPath(), $"ssltrack-expiry-{Guid.NewGuid()}.db");
        try
        {
            var factory = new TestDbFactory(file);
            using (var db = factory.CreateDbContext())
                await db.Database.MigrateAsync();
            var accounts = new AccountService(factory);
            await accounts.SaveAsync(new UserEdit { Email = "admin@example.com", DisplayName = "Admin", Role = "Admin" }, "Long-Password-123!");
            var user = (await accounts.ValidatePasswordAsync("admin@example.com", "Long-Password-123!"))!;
            var principal = AccountService.Principal(user);
            var identity = (System.Security.Claims.ClaimsIdentity)principal.Identity!;
            var expiry = identity.FindFirst("session_expires");
            if (expiry is not null)
            {
                identity.RemoveClaim(expiry);
            }

            identity.AddClaim(new("session_expires", DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds().ToString()));
            Assert.Null(await accounts.CurrentAsync(principal));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(file);
        }
    }

    private sealed class FixedAuthState(System.Security.Claims.ClaimsPrincipal principal) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(principal));
    }
}
