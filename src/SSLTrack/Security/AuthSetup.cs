namespace SSLTrack.Security;

public static partial class AuthSetup
{
    public const string Cookie = "SSLTrackCookie";
    public static IServiceCollection AddAppAuthentication(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddScoped<AccountService>();
        services.AddScoped<ServiceAccountService>();
        services.AddScoped<SmtpSettingsService>();
        services.AddScoped<AccessGuard>();
        services.AddCascadingAuthenticationState();
        services.AddScoped<AuthenticationStateProvider, RevalidatingAuthStateProvider>();
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = 429;
            o.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });
        var auth = services.AddAuthentication(o =>
        {
            o.DefaultScheme = "App";
            o.DefaultChallengeScheme = "App";
        }).AddPolicyScheme("App", "Cookie or agent Basic", o => o.ForwardDefaultSelector = c => c.Request.Headers.ContainsKey("Authorization") ? "Basic" : Cookie).AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>("Basic", null).AddCookie(Cookie, o =>
        {
            o.LoginPath = "/login";
            o.AccessDeniedPath = "/access-denied";
            o.Cookie.Name = "SSLTrack.Session";
            o.Cookie.HttpOnly = true;
            o.Cookie.SameSite = SameSiteMode.Lax;
            o.Cookie.SecurePolicy = environment.IsDevelopment() || environment.IsEnvironment("Testing") ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            o.ExpireTimeSpan = TimeSpan.FromHours(8);
            o.SlidingExpiration = false;
            o.Events.OnValidatePrincipal = async c =>
            {
                if (await c.HttpContext.RequestServices.GetRequiredService<AccountService>().CurrentAsync(c.Principal!, true) is null)
                {
                    c.RejectPrincipal();
                    await c.HttpContext.SignOutAsync(Cookie);
                }
            };
            o.Events.OnRedirectToLogin = c =>
            {
                if (IsApi(c.Request.Path))
                {
                    c.Response.StatusCode = 401;
                }
                else
                {
                    c.Response.Redirect(c.RedirectUri);
                }

                return Task.CompletedTask;
            };
            o.Events.OnRedirectToAccessDenied = c =>
            {
                if (IsApi(c.Request.Path))
                {
                    c.Response.StatusCode = 403;
                }
                else
                {
                    c.Response.Redirect(c.RedirectUri);
                }

                return Task.CompletedTask;
            };
        });
        if (configuration.GetValue<bool>("Authentication:Oidc:Enabled"))
        {
            auth.AddOpenIdConnect("oidc", o =>
            {
                o.SignInScheme = Cookie;
                o.Authority = configuration["Authentication:Oidc:Authority"];
                o.ClientId = configuration["Authentication:Oidc:ClientId"];
                o.ClientSecret = configuration["Authentication:Oidc:ClientSecret"];
                o.ResponseType = OpenIdConnectResponseType.Code;
                o.UsePkce = true;
                o.MapInboundClaims = false;
                o.SaveTokens = false;
                o.GetClaimsFromUserInfoEndpoint = false;
                o.Scope.Clear();
                o.Scope.Add("openid");
                o.Scope.Add("profile");
                o.Scope.Add("email");
                o.Events.OnTokenValidated = async c =>
                {
                    var p = c.Principal!;
                    var user = await c.HttpContext.RequestServices.GetRequiredService<AccountService>().ValidateOidcAsync(p.FindFirstValue("email"), string.Equals(p.FindFirstValue("email_verified"), "true", StringComparison.OrdinalIgnoreCase), c.SecurityToken.Issuer, p.FindFirstValue("sub") ?? "");
                    if (user is null)
                    {
                        c.Fail("Account not provisioned or identity is not allowed.");
                        return;
                    }

                    c.Principal = AccountService.Principal(user);
                };
                o.Events.OnRemoteFailure = c =>
                {
                    c.HandleResponse();
                    c.Response.Redirect("/login?error=oidc");
                    return Task.CompletedTask;
                };
            });
        }

        services.AddAuthorization(o =>
        {
            o.AddPolicy("Human", p => p.RequireAuthenticatedUser().RequireClaim("kind", "human"));
            o.AddPolicy("Admin", p => p.RequireAuthenticatedUser().RequireClaim("kind", "human").RequireRole("Admin"));
            o.AddPolicy("AgentOrHuman", p => p.RequireAuthenticatedUser().RequireClaim("kind", "human", "agent"));
            o.FallbackPolicy = o.GetPolicy("Human");
        });
        return services;
    }

    public static bool IsApi(PathString path) => path.StartsWithSegments("/api") && path != "/api";
}
