namespace SSLTrack.Models;

public class SmtpSettings
{
    public int Id { get; set; } = 1;
    public string Name { get; set; } = "";
    public string MailFrom { get; set; } = "";
    public string MailTo { get; set; } = "";
    public string? Bcc
    {
        get; set;
    }
    public string SmtpHost { get; set; } = "";
    public int Port { get; set; } = 587;
    public string? Username
    {
        get; set;
    }

    [JsonIgnore]
    public string? ProtectedPassword
    {
        get; set;
    }
    public SecureSocketOptions TlsMode { get; set; } = SecureSocketOptions.StartTls;
}

public class SmtpSettingsEdit
{
    [MaxLength(100)]
    public string? Name { get; set; } = "";

    [Required, EmailAddress]
    public string MailFrom { get; set; } = "";

    [Required]
    public string MailTo { get; set; } = "";
    public string? Bcc
    {
        get; set;
    }

    [Required, MaxLength(253)]
    public string SmtpHost { get; set; } = "";

    [Range(1, 65535)]
    public int Port { get; set; } = 587;
    public string? Username
    {
        get; set;
    }
    public string? Password
    {
        get; set;
    }
    public bool ClearPassword
    {
        get; set;
    }
    public bool HasPassword
    {
        get; set;
    }
    public SecureSocketOptions TlsMode { get; set; } = SecureSocketOptions.StartTls;
}
