namespace SSLTrack.Security;

public sealed class BasicAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, ServiceAccountService accounts, IHostEnvironment environment) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("Authorization"))
        {
            return AuthenticateResult.NoResult();
        }

        if (!Request.IsHttps && !environment.IsDevelopment() && !environment.IsEnvironment("Testing"))
        {
            return AuthenticateResult.Fail("HTTPS is required.");
        }

        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase) || header.Length > 2048)
        {
            return AuthenticateResult.Fail("Invalid credentials.");
        }

        try
        {
            var value = Encoding.UTF8.GetString(Convert.FromBase64String(header[6..]));
            var colon = value.IndexOf(':');
            if (colon < 1)
            {
                return AuthenticateResult.Fail("Invalid credentials.");
            }

            var account = await accounts.ValidateAsync(value[..colon], value[(colon + 1)..]);
            if (account is null)
            {
                return AuthenticateResult.Fail("Invalid credentials.");
            }

            var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, account.Email), new Claim("kind", "agent"), new Claim("agent_id", account.AgentId.ToString()) }, Scheme.Name);
            return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
        }
        catch (FormatException)
        {
            return AuthenticateResult.Fail("Invalid credentials.");
        }
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 401;
        Response.Headers.WWWAuthenticate = "Basic realm=\"SSLTrack agents\", charset=\"UTF-8\"";
        return Task.CompletedTask;
    }
}
