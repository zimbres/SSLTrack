namespace SSLTrack.Security;

public sealed class RevalidatingAuthStateProvider(ILoggerFactory logger, IServiceScopeFactory scopes) : RevalidatingServerAuthenticationStateProvider(logger)
{
    protected override TimeSpan RevalidationInterval => TimeSpan.FromSeconds(30);

    protected override async Task<bool> ValidateAuthenticationStateAsync(AuthenticationState state, CancellationToken token)
    {
        using var scope = scopes.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AccountService>().CurrentAsync(state.User, true) is not null;
    }
}

public sealed class AccessGuard(IServiceProvider services, IHttpContextAccessor accessor, AccountService accounts)
{
    public async Task<AppUser> RequireHumanAsync()
    {
        ClaimsPrincipal? principal = null;
        try
        {
            var provider = services.GetService<AuthenticationStateProvider>();
            if (provider is not null)
            {
                principal = (await provider.GetAuthenticationStateAsync()).User;
            }
        }
        catch (InvalidOperationException)
        {
        }

        principal ??= accessor.HttpContext?.User;
        var user = principal is null ? null : await accounts.CurrentAsync(principal);
        return user ?? throw new UnauthorizedAccessException("Your session is no longer authorized. Sign in again.");
    }

    public async Task<AppUser> RequireAdminAsync()
    {
        var user = await RequireHumanAsync();
        if (user.Role != "Admin")
        {
            throw new UnauthorizedAccessException("Administrator access is required.");
        }

        return user;
    }
}
