namespace SSLTrack.Security;

public static class Avatar
{
    public static string? Url(AppUser user) => user.AvatarType switch
    {
        "Custom" when Uri.TryCreate(user.AvatarUrl, UriKind.Absolute, out var uri) && uri.Scheme == "https" => user.AvatarUrl,
        "Gravatar" => "https://www.gravatar.com/avatar/" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(user.Email.Trim().ToLowerInvariant()))).ToLowerInvariant() + "?s=128&d=identicon",
        _ => null
    };
}
