namespace SSLTrack.Services;

public sealed class ServiceAccountService(IDbContextFactory<ApplicationDbContext> factory)
{
    private static readonly PasswordHasher<ServiceAccount> Hasher = new();
    private static readonly string DummyHash = Hasher.HashPassword(new(), "unusable-dummy-password");
    public async Task<List<ServiceAccountEdit>> ListAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.ServiceAccounts.OrderBy(x => x.Email).Select(x => new ServiceAccountEdit { Id = x.Id, Email = x.Email, AgentId = x.AgentId, Enabled = x.Enabled }).ToListAsync();
    }

    public async Task SaveAsync(ServiceAccountEdit edit, string? password)
    {
        Validator.ValidateObject(edit, new ValidationContext(edit), true);
        await using var db = await factory.CreateDbContextAsync();
        if (!await db.Agents.AnyAsync(x => x.Id == edit.AgentId && x.Id > 1))
        {
            throw new InvalidOperationException("Choose an existing remote agent.");
        }

        var normalized = AccountService.Normalize(edit.Email);
        if (await db.ServiceAccounts.AnyAsync(x => x.NormalizedEmail == normalized && x.Id != edit.Id))
        {
            throw new InvalidOperationException("A service account with this email already exists.");
        }

        var account = edit.Id == 0 ? new ServiceAccount() : await db.ServiceAccounts.SingleOrDefaultAsync(x => x.Id == edit.Id) ?? throw new InvalidOperationException("Service account not found.");
        if (edit.Id == 0)
        {
            db.ServiceAccounts.Add(account);
        }

        account.Email = edit.Email.Trim();
        account.NormalizedEmail = normalized;
        account.AgentId = edit.AgentId;
        account.Enabled = edit.Enabled;
        if (!string.IsNullOrEmpty(password))
        {
            AccountService.ValidatePassword(password);
            account.PasswordHash = Hasher.HashPassword(account, password);
            account.LockoutUntil = null;
            account.FailedAttempts = 0;
        }
        else if (string.IsNullOrEmpty(account.PasswordHash))
        {
            throw new InvalidOperationException("A password is required for a new service account.");
        }

        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var account = await db.ServiceAccounts.SingleOrDefaultAsync(x => x.Id == id) ?? throw new InvalidOperationException("Service account not found.");
        db.ServiceAccounts.Remove(account);
        await db.SaveChangesAsync();
    }

    public async Task<ServiceAccount?> ValidateAsync(string email, string password)
    {
        await using var db = await factory.CreateDbContextAsync();
        var normalized = AccountService.Normalize(email);
        var account = await db.ServiceAccounts.SingleOrDefaultAsync(x => x.NormalizedEmail == normalized);
        var result = Hasher.VerifyHashedPassword(account ?? new(), account?.PasswordHash ?? DummyHash, password);
        if (account is null || !account.Enabled || account.LockoutUntil > DateTime.UtcNow)
        {
            return null;
        }

        if (result == PasswordVerificationResult.Failed)
        {
            if (++account.FailedAttempts >= 5)
            {
                account.LockoutUntil = DateTime.UtcNow.AddMinutes(15);
                account.FailedAttempts = 0;
            }

            await db.SaveChangesAsync();
            return null;
        }

        if (!await db.Agents.AnyAsync(x => x.Id == account.AgentId && x.Id > 1))
        {
            return null;
        }

        account.FailedAttempts = 0;
        account.LockoutUntil = null;
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            account.PasswordHash = Hasher.HashPassword(account, password);
        }

        await db.SaveChangesAsync();
        return account;
    }
}
