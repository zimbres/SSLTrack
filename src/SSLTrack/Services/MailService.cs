namespace SSLTrack.Services;

public class MailService : MailProperties
{
    private readonly ILogger<MailService> _logger;
    private readonly RetryPolicy _retryPolicy;
    private readonly SmtpClient _smtpClient;

    public MailService(ILogger<MailService> logger, IConfiguration configuration, SmtpClient smtpClient)
    {
        _logger = logger;
        _smtpClient = smtpClient;

        _retryPolicy = Policy.Handle<Exception>()
            .WaitAndRetry(3, retryCount => TimeSpan.FromSeconds(Math.Pow(2, retryCount)));

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

    public void SendMail()
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

        try
        {
            _smtpClient.Connect(SmtpHost, Port, EnableSsl ? SecureSocketOptions.Auto : SecureSocketOptions.None);
            if (!string.IsNullOrWhiteSpace(Username))
            {
                _smtpClient.Authenticate(Username, Password);
            }
            _retryPolicy.Execute(() => _smtpClient.Send(message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email.");
            throw;
        }
        finally
        {
            _smtpClient.Disconnect(true);
        }
    }
}
