namespace SSLTrack.Models;

public class MailProperties
{
    public string? Name { get; set; }
    public string? MailFrom { get; set; }
    public string? MailTo { get; set; }
    public string? Bcc { get; set; }
    public string? Subject { get; set; }
    public string? Body { get; set; }
    public string? Attachment { get; set; }
    public bool IsBodyHtml { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }
    public string? SmtpHost { get; init; }
    public int Port { get; init; }
    public bool EnableSsl { get; init; }
}
