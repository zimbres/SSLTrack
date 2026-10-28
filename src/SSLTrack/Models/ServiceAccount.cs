namespace SSLTrack.Models;

public class ServiceAccount
{
    public int Id
    {
        get; set;
    }
    public string Email { get; set; } = "";
    public string NormalizedEmail { get; set; } = "";

    [JsonIgnore]
    public string PasswordHash { get; set; } = "";
    public int AgentId
    {
        get; set;
    }
    public bool Enabled { get; set; } = true;
    public int FailedAttempts
    {
        get; set;
    }
    public DateTime? LockoutUntil
    {
        get; set;
    }
}

public class ServiceAccountEdit
{
    public int Id
    {
        get; set;
    }

    [Required, EmailAddress, MaxLength(254)]
    public string Email { get; set; } = "";

    [Range(2, int.MaxValue)]
    public int AgentId
    {
        get; set;
    }
    public bool Enabled { get; set; } = true;
}
