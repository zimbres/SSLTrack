namespace SSLTrack.Services;

public sealed class SmtpSettingsService(IDbContextFactory<ApplicationDbContext> factory, IDataProtectionProvider protection)
{
    private readonly IDataProtector protector = protection.CreateProtector("SSLTrack.SMTP.Password.v1");
    public async Task ImportAsync(MailProperties? old)
    {
        await using var db = await factory.CreateDbContextAsync();
        if (await db.SmtpSettings.AnyAsync())
        {
            return;
        }

        db.SmtpSettings.Add(new SmtpSettings { Name = old?.Name ?? "", MailFrom = old?.MailFrom ?? "", MailTo = old?.MailTo ?? "", Bcc = old?.Bcc, SmtpHost = old?.SmtpHost ?? "", Port = old?.Port > 0 ? old.Port : 587, Username = old?.Username, ProtectedPassword = string.IsNullOrEmpty(old?.Password) ? null : protector.Protect(old.Password), TlsMode = old?.EnableSsl == false ? SecureSocketOptions.None : old?.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls });
        await db.SaveChangesAsync();
    }

    public async Task<SmtpSettingsEdit> GetForEditAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var s = await db.SmtpSettings.AsNoTracking().SingleOrDefaultAsync();
        return s is null ? new() : new()
        {
            Name = s.Name,
            MailFrom = s.MailFrom,
            MailTo = s.MailTo,
            Bcc = s.Bcc,
            SmtpHost = s.SmtpHost,
            Port = s.Port,
            Username = s.Username,
            TlsMode = s.TlsMode,
            HasPassword = !string.IsNullOrEmpty(s.ProtectedPassword)
        };
    }

    public async Task SaveAsync(SmtpSettingsEdit edit)
    {
        Validator.ValidateObject(edit, new ValidationContext(edit), true);
        ValidateAddresses(edit.MailTo);
        if (!string.IsNullOrWhiteSpace(edit.Bcc))
        {
            ValidateAddresses(edit.Bcc);
        }

        if (edit.TlsMode is not (SecureSocketOptions.None or SecureSocketOptions.StartTls or SecureSocketOptions.SslOnConnect))
        {
            throw new InvalidOperationException("Choose a supported TLS mode.");
        }

        if (edit.SmtpHost.Any(char.IsWhiteSpace) || edit.SmtpHost.Contains('/') || edit.SmtpHost.Contains(':'))
        {
            throw new InvalidOperationException("Enter a mail server hostname, without a scheme or port.");
        }

        await using var db = await factory.CreateDbContextAsync();
        var s = await db.SmtpSettings.SingleOrDefaultAsync();
        if (s is null)
        {
            s = new();
            db.SmtpSettings.Add(s);
        }

        s.Name = (edit.Name ?? "").Trim();
        s.MailFrom = edit.MailFrom.Trim();
        s.MailTo = edit.MailTo.Trim();
        s.Bcc = edit.Bcc;
        s.SmtpHost = edit.SmtpHost.Trim();
        s.Port = edit.Port;
        s.Username = edit.Username;
        s.TlsMode = edit.TlsMode;
        if (edit.ClearPassword)
        {
            s.ProtectedPassword = null;
        }
        else if (!string.IsNullOrEmpty(edit.Password))
        {
            s.ProtectedPassword = protector.Protect(edit.Password);
        }

        await db.SaveChangesAsync();
    }

    public async Task<(SmtpSettings Settings, string? Password)> GetForDeliveryAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var settings = await db.SmtpSettings.AsNoTracking().SingleOrDefaultAsync() ?? throw new InvalidOperationException("Configure SMTP before sending mail.");
        if (string.IsNullOrWhiteSpace(settings.SmtpHost) || string.IsNullOrWhiteSpace(settings.MailFrom) || string.IsNullOrWhiteSpace(settings.MailTo))
        {
            throw new InvalidOperationException("Configure SMTP before sending mail.");
        }

        return (settings, string.IsNullOrEmpty(settings.ProtectedPassword) ? null : protector.Unprotect(settings.ProtectedPassword));
    }

    private static void ValidateAddresses(string values)
    {
        var addresses = values.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (addresses.Length == 0 || addresses.Any(x => !new EmailAddressAttribute().IsValid(x)))
        {
            throw new InvalidOperationException("Use valid email addresses separated by semicolons.");
        }
    }
}
