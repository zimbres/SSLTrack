namespace SSLTrack.Tests;

public class AccountTests : IDisposable
{
    private readonly string file = Path.Combine(Path.GetTempPath(), $"ssltrack-tests-{Guid.NewGuid()}.db");
    private readonly AccountService accounts;
    private readonly TestDbFactory factory;
    public AccountTests()
    {
        factory = new(file);
        using var db = factory.CreateDbContext();
        db.Database.Migrate();
        accounts = new(factory);
    }

    [Fact]
    public async Task Normalized_email_is_unique()
    {
        await accounts.BootstrapAsync("admin@example.com", "Long-Password-123!");
        await Assert.ThrowsAsync<InvalidOperationException>(() => accounts.SaveAsync(new UserEdit { Email = "ADMIN@example.com", DisplayName = "Duplicate", Role = "ReadOnly" }, "Long-Password-123!"));
    }

    [Fact]
    public async Task Bootstrap_does_not_replace_existing_admin()
    {
        await accounts.BootstrapAsync("admin@example.com", "Long-Password-123!");
        await accounts.BootstrapAsync("other@example.com", "Other-Password-123!");
        Assert.Single(await accounts.ListAsync());
        Assert.Null(await accounts.ValidatePasswordAsync("other@example.com", "Other-Password-123!"));
    }

    [Fact]
    public async Task Wrong_password_locks_account_and_email_is_case_insensitive()
    {
        await accounts.BootstrapAsync("admin@example.com", "Long-Password-123!");
        Assert.NotNull(await accounts.ValidatePasswordAsync("ADMIN@example.com", "Long-Password-123!"));
        for (var i = 0; i < 5; i++)
            Assert.Null(await accounts.ValidatePasswordAsync("admin@example.com", "wrong"));
        Assert.Null(await accounts.ValidatePasswordAsync("admin@example.com", "Long-Password-123!"));
    }

    [Fact]
    public async Task Last_enabled_admin_cannot_be_disabled()
    {
        await accounts.BootstrapAsync("admin@example.com", "Long-Password-123!");
        var user = Assert.Single(await accounts.ListAsync());
        user.Enabled = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => accounts.SaveAsync(user, null));
    }

    [Fact]
    public async Task Oidc_requires_precreated_verified_account_and_binds_subject()
    {
        Assert.Null(await accounts.ValidateOidcAsync("missing@example.com", true, "issuer", "subject"));
        await accounts.SaveAsync(new UserEdit { Email = "oidc@example.com", DisplayName = "OIDC", AuthenticationType = "Oidc", Role = "ReadOnly" }, null);
        Assert.Null(await accounts.ValidateOidcAsync("oidc@example.com", false, "issuer", "subject"));
        Assert.NotNull(await accounts.ValidateOidcAsync("OIDC@example.com", true, "issuer", "subject"));
        Assert.Null(await accounts.ValidateOidcAsync("oidc@example.com", true, "issuer", "other"));
    }

    [Fact]
    public async Task Disabled_user_and_required_password_change_are_enforced()
    {
        var edit = new UserEdit
        {
            Email = "user@example.com",
            DisplayName = "User",
            Role = "ReadOnly",
            RequirePasswordChange = true
        };
        await accounts.SaveAsync(edit, "Long-Password-123!");
        var user = await accounts.ValidatePasswordAsync(edit.Email, "Long-Password-123!");
        Assert.True(user!.RequirePasswordChange);
        await accounts.ChangePasswordAsync(user.Id, "Long-Password-123!", "Changed-Password-123!");
        Assert.False((await accounts.FindAsync(user.Id))!.RequirePasswordChange);
        edit = Assert.Single(await accounts.ListAsync());
        edit.Enabled = false;
        await accounts.SaveAsync(edit, null);
        Assert.Null(await accounts.ValidatePasswordAsync(edit.Email, "Changed-Password-123!"));
    }

    [Fact]
    public async Task Existing_user_email_is_immutable()
    {
        await accounts.SaveAsync(new UserEdit { Email = "original@example.com", DisplayName = "User", Role = "ReadOnly" }, "Long-Password-123!");
        var edit = Assert.Single(await accounts.ListAsync());
        edit.Email = "changed@example.com";
        await Assert.ThrowsAsync<InvalidOperationException>(() => accounts.SaveAsync(edit, null));
        Assert.Equal("original@example.com", Assert.Single(await accounts.ListAsync()).Email);
    }

    [Fact]
    public async Task Deleting_user_revokes_session_and_releases_oidc_identity()
    {
        await accounts.SaveAsync(new UserEdit { Email = "oidc@example.com", DisplayName = "OIDC", AuthenticationType = "Oidc" }, null);
        var user = (await accounts.ValidateOidcAsync("oidc@example.com", true, "issuer", "subject"))!;
        var principal = AccountService.Principal(user);
        await accounts.DeleteAsync(user.Id);
        Assert.Null(await accounts.FindAsync(user.Id));
        Assert.Null(await accounts.CurrentAsync(principal));
        await accounts.SaveAsync(new UserEdit { Email = "oidc@example.com", DisplayName = "Replacement", AuthenticationType = "Oidc" }, null);
        Assert.NotNull(await accounts.ValidateOidcAsync("oidc@example.com", true, "issuer", "subject"));
    }

    [Fact]
    public async Task Last_enabled_admin_cannot_be_deleted_but_another_admin_can()
    {
        await accounts.BootstrapAsync("admin@example.com", "Long-Password-123!");
        var admin = Assert.Single(await accounts.ListAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => accounts.DeleteAsync(admin.Id));
        Assert.NotNull(await accounts.FindAsync(admin.Id));
        await accounts.SaveAsync(new UserEdit { Email = "other@example.com", DisplayName = "Other", Role = "Admin", AuthenticationType = "Oidc" }, null);
        await accounts.DeleteAsync(admin.Id);
        Assert.Null(await accounts.FindAsync(admin.Id));
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(file);
    }
}

public sealed class TestDbFactory(string file) : IDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext() => new(new DbContextOptionsBuilder<ApplicationDbContext>().Options, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Data"] = $"Data Source={file}" }).Build());
}
