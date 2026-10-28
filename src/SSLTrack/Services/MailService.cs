namespace SSLTrack.Services;

public sealed class MailService(ILogger<MailService> logger, SmtpSettingsService settings) : MailProperties
{
    public async Task SendMailAsync()
    {
        var (smtp, password) = await settings.GetForDeliveryAsync();
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(smtp.Name, smtp.MailFrom));
        foreach (var address in smtp.MailTo.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            message.To.Add(MailboxAddress.Parse(address));
        }

        foreach (var address in (smtp.Bcc ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            message.Bcc.Add(MailboxAddress.Parse(address));
        }
        message.Subject = Subject;
        message.Body = new TextPart(IsBodyHtml ? "html" : "plain")
        {
            Text = Body
        };
        var retry = Policy.Handle<Exception>().WaitAndRetryAsync(2, i => TimeSpan.FromSeconds(Math.Pow(2, i)));
        try
        {
            await retry.ExecuteAsync(async () =>
            {
                using var client = new SmtpClient();
                await client.ConnectAsync(smtp.SmtpHost, smtp.Port, smtp.TlsMode);
                if (!string.IsNullOrWhiteSpace(smtp.Username))
                {
                    await client.AuthenticateAsync(smtp.Username, password ?? "");
                }

                await client.SendAsync(message);
                await client.DisconnectAsync(true);
            });
        }
        catch (Exception ex)
        {
            logger.LogError("SMTP delivery failed ({ErrorType}). Check mail server configuration.", ex.GetType().Name);
            throw;
        }
    }
}
