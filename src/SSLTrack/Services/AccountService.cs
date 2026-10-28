namespace SSLTrack.Services;
// Persistence/admission primitive. Browser management calls are guarded by AccountController.
public sealed class AccountService(IDbContextFactory<ApplicationDbContext> factory)
{
    private static readonly PasswordHasher<AppUser> Hasher = new();
    private static readonly string DummyHash = Hasher.HashPassword(new(), "unusable-dummy-password");
    public static string Normalize(string email) => email.Trim().ToUpperInvariant();
    public static void ValidatePassword(string password)
    {
        if (password.Length < 12 || password.Length > 128)
        {
            throw new InvalidOperationException("Use a password between 12 and 128 characters.");
        }
    }

    public async Task BootstrapAsync(string? email, string? password)
    {
        await using var db = await factory.CreateDbContextAsync();
        if (await db.Users.AnyAsync())
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        await SaveAsync(new UserEdit { Email = email, DisplayName = "Administrator", Role = "Admin", RequirePasswordChange = true }, password);
    }

    public async Task<AppUser?> FindAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
    }

    public async Task<List<UserEdit>> ListAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Users.OrderBy(x => x.Email).Select(x => new UserEdit { Id = x.Id, Email = x.Email, DisplayName = x.DisplayName, Role = x.Role, AuthenticationType = x.AuthenticationType, Enabled = x.Enabled, RequirePasswordChange = x.RequirePasswordChange }).ToListAsync();
    }

    public async Task SaveAsync(UserEdit edit, string? password)
    {
        Validator.ValidateObject(edit, new ValidationContext(edit), true);
        if (edit.Role is not ("Admin" or "ReadOnly") || edit.AuthenticationType is not ("Local" or "Oidc"))
        {
            throw new InvalidOperationException("Invalid role or sign-in method.");
        }

        await using var db = await factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var normalized = Normalize(edit.Email);
        if (await db.Users.AnyAsync(x => x.NormalizedEmail == normalized && x.Id != edit.Id))
        {
            throw new InvalidOperationException("A user with this email already exists.");
        }

        var user = edit.Id == 0 ? new AppUser() : await db.Users.SingleOrDefaultAsync(x => x.Id == edit.Id) ?? throw new InvalidOperationException("User not found.");
        if (user.Id != 0 && user.NormalizedEmail != normalized)
        {
            throw new InvalidOperationException("Email is the account ID and cannot be changed after creation.");
        }

        if (user.Id != 0 && user.Enabled && user.Role == "Admin" && (!edit.Enabled || edit.Role != "Admin") && !await db.Users.AnyAsync(x => x.Id != user.Id && x.Enabled && x.Role == "Admin"))
        {
            throw new InvalidOperationException("Keep at least one enabled administrator.");
        }

        if (user.Id == 0)
        {
            db.Users.Add(user);
        }

        if (user.NormalizedEmail != normalized || user.AuthenticationType != edit.AuthenticationType)
        {
            user.OidcIssuer = null;
            user.OidcSubject = null;
        }

        user.Email = edit.Id == 0 ? edit.Email.Trim() : user.Email;
        user.NormalizedEmail = normalized;
        user.DisplayName = edit.DisplayName.Trim();
        user.Role = edit.Role;
        user.Enabled = edit.Enabled;
        user.AuthenticationType = edit.AuthenticationType;
        user.RequirePasswordChange = edit.AuthenticationType == "Local" && edit.RequirePasswordChange;
        if (edit.AuthenticationType == "Oidc")
        {
            user.PasswordHash = null;
        }
        else if (!string.IsNullOrEmpty(password))
        {
            ValidatePassword(password);
            user.PasswordHash = Hasher.HashPassword(user, password);
            user.FailedAttempts = 0;
            user.LockoutUntil = null;
        }
        else if (user.PasswordHash is null)
        {
            throw new InvalidOperationException("A password is required for a local user.");
        }

        user.SecurityStamp = Guid.NewGuid().ToString();
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task<AppUser?> ValidatePasswordAsync(string email, string password)
    {
        await using var db = await factory.CreateDbContextAsync();
        var normalized = Normalize(email);
        var user = await db.Users.SingleOrDefaultAsync(x => x.NormalizedEmail == normalized);
        var result = Hasher.VerifyHashedPassword(user ?? new(), user?.PasswordHash ?? DummyHash, password);
        if (user is null || !user.Enabled || user.AuthenticationType != "Local" || user.LockoutUntil > DateTime.UtcNow)
        {
            return null;
        }

        if (result == PasswordVerificationResult.Failed)
        {
            user.FailedAttempts++;
            if (user.FailedAttempts >= 5)
            {
                user.LockoutUntil = DateTime.UtcNow.AddMinutes(15);
                user.FailedAttempts = 0;
            }

            await db.SaveChangesAsync();
            return null;
        }

        user.FailedAttempts = 0;
        user.LockoutUntil = null;
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = Hasher.HashPassword(user, password);
        }

        await db.SaveChangesAsync();
        return user;
    }

    public async Task<AppUser?> ValidateOidcAsync(string? email, bool verified, string issuer, string subject)
    {
        if (!verified || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(subject))
        {
            return null;
        }

        await using var db = await factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var normalized = Normalize(email);
        var user = await db.Users.SingleOrDefaultAsync(x => x.NormalizedEmail == normalized);
        if (user is null || !user.Enabled || user.AuthenticationType != "Oidc")
        {
            return null;
        }

        if (user.OidcSubject is not null && (user.OidcSubject != subject || user.OidcIssuer != issuer))
        {
            return null;
        }

        if (await db.Users.AnyAsync(x => x.Id != user.Id && x.OidcIssuer == issuer && x.OidcSubject == subject))
        {
            return null;
        }

        user.OidcIssuer = issuer;
        user.OidcSubject = subject;
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return user;
    }

    public async Task ChangePasswordAsync(int id, string current, string password)
    {
        ValidatePassword(password);
        await using var db = await factory.CreateDbContextAsync();
        var user = await db.Users.SingleAsync(x => x.Id == id);
        if (!user.Enabled || user.AuthenticationType != "Local" || user.PasswordHash is null || Hasher.VerifyHashedPassword(user, user.PasswordHash, current) == PasswordVerificationResult.Failed)
        {
            throw new InvalidOperationException("Current password is incorrect.");
        }

        if (current == password)
        {
            throw new InvalidOperationException("Choose a different password.");
        }

        user.PasswordHash = Hasher.HashPassword(user, password);
        user.RequirePasswordChange = false;
        user.SecurityStamp = Guid.NewGuid().ToString();
        user.FailedAttempts = 0;
        user.LockoutUntil = null;
        await db.SaveChangesAsync();
    }

    public async Task UpdateProfileAsync(int id, string name, string avatarType, string? avatarUrl)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
        {
            throw new InvalidOperationException("Enter a display name of up to 100 characters.");
        }

        if (avatarType is not ("Default" or "Gravatar" or "Custom"))
        {
            throw new InvalidOperationException("Choose an avatar type.");
        }

        if (avatarType == "Custom" && (!Uri.TryCreate(avatarUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo) || avatarUrl!.Length > 2048))
        {
            throw new InvalidOperationException("Enter an HTTPS picture URL.");
        }

        await using var db = await factory.CreateDbContextAsync();
        var user = await db.Users.SingleAsync(x => x.Id == id);
        user.DisplayName = name.Trim();
        user.AvatarType = avatarType;
        user.AvatarUrl = avatarType == "Custom" ? avatarUrl : null;
        await db.SaveChangesAsync();
    }

    public static ClaimsPrincipal Principal(AppUser user) => new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, user.Email), new Claim("user_id", user.Id.ToString()), new Claim(ClaimTypes.Name, user.DisplayName), new Claim(ClaimTypes.Email, user.Email), new Claim(ClaimTypes.Role, user.Role), new Claim("stamp", user.SecurityStamp), new Claim("kind", "human"), new Claim("session_expires", DateTimeOffset.UtcNow.AddHours(8).ToUnixTimeSeconds().ToString()), new Claim("password_change", user.RequirePasswordChange.ToString()) }, AuthSetup.Cookie));
    public async Task<AppUser?> CurrentAsync(ClaimsPrincipal principal, bool allowPasswordChange = false)
    {
        if (principal.Identity?.IsAuthenticated != true || principal.FindFirstValue("kind") != "human" || !int.TryParse(principal.FindFirstValue("user_id"), out var id))
        {
            return null;
        }

        if (!long.TryParse(principal.FindFirstValue("session_expires"), out var expiry) || expiry <= DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        {
            return null;
        }

        var user = await FindAsync(id);
        return user is { Enabled: true } && user.SecurityStamp == principal.FindFirstValue("stamp") && (allowPasswordChange || !user.RequirePasswordChange) ? user : null;
    }
}
