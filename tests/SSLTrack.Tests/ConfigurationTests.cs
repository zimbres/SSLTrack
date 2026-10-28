namespace SSLTrack.Tests;

public class ConfigurationTests : IDisposable
{
    private readonly string file = Path.Combine(Path.GetTempPath(), $"ssltrack-config-{Guid.NewGuid()}.db");
    private readonly TestDbFactory factory;
    public ConfigurationTests()
    {
        factory = new(file);
        using var db = factory.CreateDbContext();
        db.Database.Migrate();
    }

    [Fact]
    public async Task Service_accounts_require_real_agent_and_rotation_revokes_old_secret()
    {
        var service = new ServiceAccountService(factory);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(new ServiceAccountEdit { Email = "agent@example.com", AgentId = 999 }, "Long-Password-123!"));
        using (var db = factory.CreateDbContext())
        {
            db.Agents.Add(new Agent { Id = 42, Name = "Test" });
            await db.SaveChangesAsync();
        }

        await service.SaveAsync(new ServiceAccountEdit { Email = "agent@example.com", AgentId = 42 }, "Long-Password-123!");
        var account = await service.ValidateAsync("AGENT@example.com", "Long-Password-123!");
        Assert.Equal(42, account!.AgentId);
        var edit = Assert.Single(await service.ListAsync());
        await service.SaveAsync(edit, "Rotated-Password-123!");
        Assert.Null(await service.ValidateAsync(edit.Email, "Long-Password-123!"));
        Assert.NotNull(await service.ValidateAsync(edit.Email, "Rotated-Password-123!"));
        edit.Enabled = false;
        await service.SaveAsync(edit, null);
        Assert.Null(await service.ValidateAsync(edit.Email, "Rotated-Password-123!"));
    }

    [Fact]
    public async Task Smtp_password_is_protected_preserved_and_explicitly_cleared()
    {
        var service = new SmtpSettingsService(factory, new EphemeralDataProtectionProvider());
        var edit = new SmtpSettingsEdit
        {
            MailFrom = "from@example.com",
            MailTo = "to@example.com",
            SmtpHost = "localhost",
            Password = "smtp-secret"
        };
        await service.SaveAsync(edit);
        using (var db = factory.CreateDbContext())
            Assert.NotEqual("smtp-secret", (await db.SmtpSettings.SingleAsync()).ProtectedPassword);
        edit = await service.GetForEditAsync();
        Assert.Null(edit.Password);
        Assert.True(edit.HasPassword);
        edit.SmtpHost = "mail.example.com";
        await service.SaveAsync(edit);
        Assert.Equal("smtp-secret", (await service.GetForDeliveryAsync()).Password);
        edit.ClearPassword = true;
        await service.SaveAsync(edit);
        Assert.Null((await service.GetForDeliveryAsync()).Password);
    }

    [Fact]
    public async Task Smtp_import_runs_once_and_invalid_settings_are_rejected()
    {
        var service = new SmtpSettingsService(factory, new EphemeralDataProtectionProvider());
        await service.ImportAsync(new MailProperties { MailFrom = "from@example.com", MailTo = "to@example.com", SmtpHost = "old", Port = 25 });
        await service.ImportAsync(new MailProperties { SmtpHost = "replacement" });
        var edit = await service.GetForEditAsync();
        Assert.Equal("old", edit.SmtpHost);
        edit.Port = 0;
        await Assert.ThrowsAnyAsync<Exception>(() => service.SaveAsync(edit));
        edit.Port = 25;
        edit.MailTo = "bad address";
        await Assert.ThrowsAnyAsync<Exception>(() => service.SaveAsync(edit));
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(file);
    }
}
