namespace SSLTrack.Tests;

public class ApiTests : IDisposable
{
    private readonly string file = Path.Combine(Path.GetTempPath(), $"ssltrack-api-{Guid.NewGuid()}.db");
    private readonly WebApplicationFactory<ApplicationDbContext> app;
    public ApiTests()
    {
        app = new WebApplicationFactory<ApplicationDbContext>().WithWebHostBuilder(b => b.UseEnvironment("Testing").UseDefaultServiceProvider(o =>
        {
            o.ValidateScopes = true;
            o.ValidateOnBuild = true;
        }).ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Data"] = $"Data Source={file}", ["Authentication:AllowInsecureBasicForTesting"] = "true" })));
    }

    private HttpClient Client() => app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
    private async Task Seed()
    {
        _ = app.Server;
        using var scope = app.Services.CreateScope();
        var accounts = scope.ServiceProvider.GetRequiredService<AccountService>();
        await accounts.SaveAsync(new UserEdit { Email = "admin@example.com", DisplayName = "Admin", Role = "Admin" }, "Long-Password-123!");
        await accounts.SaveAsync(new UserEdit { Email = "reader@example.com", DisplayName = "Reader", Role = "ReadOnly" }, "Long-Password-123!");
        await accounts.SaveAsync(new UserEdit { Email = "change@example.com", DisplayName = "Change", Role = "ReadOnly", RequirePasswordChange = true }, "Long-Password-123!");
        using var db = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext();
        db.Agents.AddRange(new Agent { Id = 42, Name = "Agent A" }, new Agent { Id = 43, Name = "Agent B" });
        db.Domains.AddRange(new Domain { DomainName = "a.example.com", Agent = 42, Port = 443 }, new Domain { DomainName = "b.example.com", Agent = 43, Port = 443 });
        await db.SaveChangesAsync();
        await scope.ServiceProvider.GetRequiredService<ServiceAccountService>().SaveAsync(new ServiceAccountEdit { Email = "agent@example.com", AgentId = 42 }, "Long-Password-123!");
    }

    private static async Task<HttpResponseMessage> Login(HttpClient client, string email, string returnUrl = "/", string password = "Long-Password-123!")
    {
        var html = await client.GetStringAsync("/login");
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(token);
        var response = await client.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string, string> { ["email"] = email, ["password"] = password, ["returnUrl"] = returnUrl, ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token) }));
        Assert.True(response.StatusCode == HttpStatusCode.Redirect, await response.Content.ReadAsStringAsync());
        return response;
    }

    [Fact]
    public async Task Anonymous_api_is_401_and_health_is_public()
    {
        using var client = Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/domains")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/health")).StatusCode);
    }

    [Fact]
    public async Task Reader_can_read_but_cannot_write_and_browser_writes_need_antiforgery()
    {
        await Seed();
        using var client = Client();
        Assert.Equal(HttpStatusCode.Redirect, (await Login(client, "reader@example.com")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/domains")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/domains/update", null)).StatusCode);
        using var admin = Client();
        await Login(admin, "admin@example.com");
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/api/domains/update", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/account/logout", null)).StatusCode);
    }

    [Fact]
    public async Task Forced_password_change_blocks_monitoring_and_return_urls_are_local()
    {
        await Seed();
        using var client = Client();
        var result = await Login(client, "change@example.com", "https://evil.example");
        Assert.Equal("/change-password", result.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/domains")).StatusCode);
        using var admin = Client();
        result = await Login(admin, "admin@example.com", "//evil.example");
        Assert.Equal("/", result.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Revoked_cookie_loses_access()
    {
        await Seed();
        using var client = Client();
        await Login(client, "reader@example.com");
        using var scope = app.Services.CreateScope();
        var accounts = scope.ServiceProvider.GetRequiredService<AccountService>();
        var user = (await accounts.ListAsync()).Single(x => x.Email == "reader@example.com");
        user.Enabled = false;
        await accounts.SaveAsync(user, null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/domains")).StatusCode);
    }

    [Fact]
    public async Task Basic_account_is_scoped_and_reports_cannot_change_management_fields()
    {
        await Seed();
        using var client = Client();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes("agent@example.com:Long-Password-123!")));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/domains/agents/42")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/domains/agents/43")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/domains")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/logs/clear", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/logs", new Log { Agent = 43, Domain = "b.example.com", Message = "test" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, "/api/ping/43"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync("/api/domains/b.example.com", new Domain { DomainName = "b.example.com", Agent = 43 })).StatusCode);
        var report = new Domain
        {
            Id = 999,
            DomainName = "a.example.com",
            Agent = 43,
            Port = 444,
            Silenced = true,
            UserId = "attacker",
            Issuer = "New issuer",
            ExpiryDate = DateTime.Today.AddDays(30),
            LastChecked = DateTime.Now
        };
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync("/api/domains/a.example.com", report)).StatusCode);
        using var scope = app.Services.CreateScope();
        using var db = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext();
        var domain = await db.Domains.SingleAsync(x => x.DomainName == "a.example.com");
        Assert.Equal(42, domain.Agent);
        Assert.Equal(443, domain.Port);
        Assert.False(domain.Silenced);
        Assert.Equal("User", domain.UserId);
        Assert.Equal("New issuer", domain.Issuer);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/users")).StatusCode);
    }

    [Fact]
    public async Task Admin_pages_render_and_profile_changes_do_not_change_role()
    {
        await Seed();
        using var client = Client();
        await Login(client, "admin@example.com");
        foreach (var path in new[]
        {
            "/users",
            "/service-accounts",
            "/notifications",
            "/profile",
            "/change-password",
            "/domains",
            "/agents",
            "/logs",
            "/api/swagger/v1/swagger.json"
        }

        )
        {
            var response = await client.GetAsync(path);
            Assert.True(response.StatusCode == HttpStatusCode.OK, path + ": " + await response.Content.ReadAsStringAsync());
        }

        using var reader = Client();
        await Login(reader, "reader@example.com");
        foreach (var path in new[]
        {
            "/users",
            "/service-accounts",
            "/notifications"
        }

        )
        {
            Assert.Equal(HttpStatusCode.Redirect, (await reader.GetAsync(path)).StatusCode);
        }
        var form = await reader.GetStringAsync("/profile");
        var token = WebUtility.HtmlDecode(Regex.Match(form, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        var saved = await reader.PostAsync("/account/profile", new FormUrlEncodedContent(new Dictionary<string, string> { ["displayName"] = "Updated Reader", ["avatarType"] = "Custom", ["avatarUrl"] = "https://example.com/picture.png", ["Role"] = "Admin", ["Email"] = "changed@example.com", ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        using var scope = app.Services.CreateScope();
        var user = (await scope.ServiceProvider.GetRequiredService<AccountService>().ListAsync()).Single(x => x.Email == "reader@example.com");
        Assert.Equal("ReadOnly", user.Role);
        Assert.Equal("Updated Reader", user.DisplayName);
        Assert.Equal("reader@example.com", user.Email);
    }

    [Fact]
    public async Task Ambiguous_agent_reports_are_rejected_and_bad_credentials_are_401()
    {
        await Seed();
        using var client = Client();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes("agent@example.com:wrong")));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/domains/agents/42")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes("agent@example.com:Long-Password-123!")));
        using var scope = app.Services.CreateScope();
        using var db = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext();
        db.Domains.AddRange(new Domain { DomainName = "a.example.com", Agent = 42, Port = 443 }, new Domain { DomainName = "a.example.com", Agent = 42, Port = 8443 });
        await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync("/api/domains/a.example.com", new Domain { DomainName = "a.example.com", Port = 443 })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync("/api/domains/a.example.com", new Domain { DomainName = "a.example.com", Port = 8443 })).StatusCode);
    }

    [Fact]
    public async Task Password_change_flow_clears_restriction_and_requires_new_login()
    {
        await Seed();
        using var client = Client();
        await Login(client, "change@example.com");
        var html = await client.GetStringAsync("/change-password");
        var token = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        var response = await client.PostAsync("/account/password", new FormUrlEncodedContent(new Dictionary<string, string> { ["currentPassword"] = "Long-Password-123!", ["newPassword"] = "Changed-Password-123!", ["confirmPassword"] = "Changed-Password-123!", ["__RequestVerificationToken"] = token }));
        Assert.Equal("/login?message=password-changed", response.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/domains")).StatusCode);
        await Login(client, "change@example.com", password: "Changed-Password-123!");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/domains")).StatusCode);
    }

    [Fact]
    public async Task Smtp_form_accepts_optional_sender_name_and_never_echoes_password()
    {
        await Seed();
        using var client = Client();
        await Login(client, "admin@example.com");
        var html = await client.GetStringAsync("/notifications");
        var token = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        var response = await client.PostAsync("/account/smtp", new FormUrlEncodedContent(new Dictionary<string, string> { ["Name"] = "", ["MailFrom"] = "sender@example.com", ["MailTo"] = "recipient@example.com", ["SmtpHost"] = "localhost", ["Port"] = "25", ["TlsMode"] = "None", ["Password"] = "mail-secret", ["__RequestVerificationToken"] = token }));
        Assert.Equal("/notifications?saved=true", response.Headers.Location!.OriginalString);
        html = await client.GetStringAsync("/notifications");
        Assert.DoesNotContain("mail-secret", html);
        Assert.Contains("A password is saved", html);
    }

    [Fact]
    public async Task Admin_can_remove_an_agent_service_account()
    {
        await Seed();
        using var client = Client();
        await Login(client, "admin@example.com");
        var html = await client.GetStringAsync("/service-accounts");
        var token = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        using var scope = app.Services.CreateScope();
        var account = Assert.Single(await scope.ServiceProvider.GetRequiredService<ServiceAccountService>().ListAsync());
        var response = await client.PostAsync("/account/service-accounts/delete", new FormUrlEncodedContent(new Dictionary<string, string> { ["id"] = account.Id.ToString(), ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<ServiceAccountService>().ListAsync());
    }

    [Fact]
    public async Task Existing_agent_auth_service_produces_accepted_basic_header()
    {
        await Seed();
        using var client = Client();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Configurations:AuthType"] = "Basic", ["Configurations:Username"] = "agent@example.com", ["Configurations:Password"] = "Long-Password-123!" }).Build();
        var auth = new SSLTrackAgent.Services.AuthService(NullLogger<SSLTrackAgent.Services.AuthService>.Instance, config, app.Services.GetRequiredService<IHttpClientFactory>());
        client.DefaultRequestHeaders.Authorization = await auth.GetAuthHeaderAsync();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/domains/agents/42")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/domains/agents/43")).StatusCode);
    }

    [Fact]
    public async Task Admin_can_disable_user_and_list_hides_disabled_unless_requested()
    {
        await Seed();
        using var client = Client();
        await Login(client, "admin@example.com");
        using var scope = app.Services.CreateScope();
        var accounts = scope.ServiceProvider.GetRequiredService<AccountService>();
        var reader = (await accounts.ListAsync()).Single(x => x.Email == "reader@example.com");
        var html = await client.GetStringAsync("/users?edit=" + reader.Id);
        var token = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        var response = await client.PostAsync("/account/users", new FormUrlEncodedContent(new Dictionary<string, string> { ["Id"] = reader.Id.ToString(), ["Email"] = reader.Email, ["DisplayName"] = reader.DisplayName, ["Role"] = reader.Role, ["AuthenticationType"] = reader.AuthenticationType, ["Enabled"] = "false", ["RequirePasswordChange"] = "false", ["__RequestVerificationToken"] = token }));
        Assert.Equal("/users?saved=true", response.Headers.Location!.OriginalString);
        Assert.False((await accounts.ListAsync()).Single(x => x.Id == reader.Id).Enabled);
        Assert.DoesNotContain("reader@example.com", await client.GetStringAsync("/users"));
        var all = await client.GetStringAsync("/users?showDisabled=true");
        Assert.Contains("reader@example.com", all);
        Assert.Contains("Show disabled users", all);
        using var disabled = Client();
        var login = await Login(disabled, "reader@example.com");
        Assert.Equal("/login?error=credentials", login.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Rendered_forms_keep_the_named_fields_required_by_browser_posts()
    {
        await Seed();
        using var client = Client();
        var login = await client.GetStringAsync("/login");
        foreach (var name in new[]
        {
            "email",
            "password",
            "returnUrl"
        }

        )
        {
            AssertInput(login, name, "/login");
        }
        await Login(client, "admin@example.com");
        var contracts = new Dictionary<string, string[]>
        {
            ["/users"] = ["Id", "Email", "DisplayName", "Role", "AuthenticationType", "password", "Enabled", "RequirePasswordChange"],
            ["/service-accounts"] = ["Id", "Email", "AgentId", "password", "Enabled"],
            ["/notifications"] = ["Name", "MailFrom", "MailTo", "Bcc", "SmtpHost", "Port", "TlsMode", "Username", "Password", "ClearPassword"],
            ["/profile"] = ["displayName", "avatarType", "avatarUrl"],
            ["/change-password"] = ["currentPassword", "newPassword", "confirmPassword"]
        };
        foreach (var (path, names) in contracts)
        {
            var html = await client.GetStringAsync(path);
            foreach (var name in names)
            {
                AssertInput(html, name, path);
            }
        }

        static void AssertInput(string html, string name, string path)
        {
            var pattern = "<input\\b[^>]*\\bname=\"" + Regex.Escape(name) + "\"";
            Assert.True(Regex.IsMatch(html, pattern, RegexOptions.IgnoreCase), $"Missing posted input {name} on {path}");
        }
    }

    public void Dispose()
    {
        app.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(file);
    }
}
