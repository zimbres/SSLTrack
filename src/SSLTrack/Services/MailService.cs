namespace SSLTrack.Services;

public class MailService : MailProperties
{
    private readonly ILogger<MailService> _logger;
    private readonly AsyncRetryPolicy _retryPolicy = Policy.Handle<Exception>()
            .WaitAndRetryAsync(2, retryCount => TimeSpan.FromSeconds(Math.Pow(2, retryCount)));

    public MailService(ILogger<MailService> logger, IConfiguration configuration)
    {
        _logger = logger;

        var mailProperties = configuration.GetSection("MailProperties").Get<MailProperties>()
                             ?? throw new ArgumentNullException(nameof(configuration), "MailProperties configuration is missing.");

        MailFrom = mailProperties.MailFrom;
        MailTo = mailProperties.MailTo;
        Bcc = mailProperties.Bcc;
        Subject = mailProperties.Subject;
        Body = mailProperties.Body;
        Attachment = mailProperties.Attachment;
        IsBodyHtml = mailProperties.IsBodyHtml;
        Name = mailProperties.Name;
        Username = mailProperties.Username;
        Password = mailProperties.Password;
        SmtpHost = mailProperties.SmtpHost;
        Port = mailProperties.Port;
        EnableSsl = mailProperties.EnableSsl;
    }

    public async Task SendMailAsync()
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(Name, MailFrom));
        foreach (var recipient in MailTo.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            message.To.Add(new MailboxAddress(recipient, recipient));
        }
        if (!string.IsNullOrEmpty(Bcc))
        {
            foreach (var bccRecipient in Bcc.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                message.Bcc.Add(new MailboxAddress(bccRecipient, bccRecipient));
            }
        }
        message.Subject = Subject;
        message.Body = IsBodyHtml
            ? new TextPart("html") { Text = Body }
            : new TextPart("plain") { Text = Body };

        using var client = new SmtpClient();
        try
        {
            await _retryPolicy.ExecuteAsync(async () =>
            {
                await client.ConnectAsync(SmtpHost, Port, EnableSsl ? SecureSocketOptions.Auto : SecureSocketOptions.None);
                if (!string.IsNullOrWhiteSpace(Username))
                {
                    await client.AuthenticateAsync(Username, Password);
                }
                await client.SendAsync(message);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email.");
            throw;
        }
        finally
        {
            await client.DisconnectAsync(true);
        }
    }
}
