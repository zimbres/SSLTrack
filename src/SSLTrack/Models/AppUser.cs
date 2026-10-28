namespace SSLTrack.Models;

public class AppUser
{
    public int Id
    {
        get; set;
    }
    public string Email { get; set; } = "";
    public string NormalizedEmail { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Role { get; set; } = "ReadOnly";
    public string AuthenticationType { get; set; } = "Local";
    public bool Enabled { get; set; } = true;
    public bool RequirePasswordChange
    {
        get; set;
    }

    [JsonIgnore]
    public string? PasswordHash
    {
        get; set;
    }

    [JsonIgnore]
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString();

    [JsonIgnore]
    public string? OidcIssuer
    {
        get; set;
    }

    [JsonIgnore]
    public string? OidcSubject
    {
        get; set;
    }
    public int FailedAttempts
    {
        get; set;
    }
    public DateTime? LockoutUntil
    {
        get; set;
    }
    public string AvatarType { get; set; } = "Default";
    public string? AvatarUrl
    {
        get; set;
    }
}

public class UserEdit
{
    public int Id
    {
        get; set;
    }

    [Required, EmailAddress, MaxLength(254)]
    public string Email { get; set; } = "";

    [Required, MaxLength(100)]
    public string DisplayName { get; set; } = "";

    [RegularExpression("^(Admin|ReadOnly)$")]
    public string Role { get; set; } = "ReadOnly";

    [RegularExpression("^(Local|Oidc)$")]
    public string AuthenticationType { get; set; } = "Local";
    public bool Enabled { get; set; } = true;
    public bool RequirePasswordChange
    {
        get; set;
    }
}
