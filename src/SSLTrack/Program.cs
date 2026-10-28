var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddHangfire(options => options.UseInMemoryStorage());
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddHangfireServer();
    builder.Services.AddHostedService<HangfireService>();
}

builder.Services.AddMudServices();
builder.Services.AddControllersWithViews();
builder.Services.AddSwaggerGen(c => c.SwaggerDoc("v1", new OpenApiInfo { Title = "SSLTrack", Version = "v1", Description = "Human cookie sessions: Admin writes, ReadOnly reads. Agent HTTP Basic: assigned domains, certificate reports, logs and heartbeat only. Browser writes require antiforgery tokens." }));
builder.Services.AddDataProtection().PersistKeysToDbContext<ApplicationDbContext>();
builder.Services.AddDbContextFactory<ApplicationDbContext>();
builder.Services.AddAppAuthentication(builder.Configuration, builder.Environment);
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    foreach (var proxy in builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
    {
        if (!IPAddress.TryParse(proxy, out var address))
        {
            throw new InvalidOperationException("ReverseProxy:KnownProxies must contain IP addresses.");
        }

        options.KnownProxies.Add(address);
    }
});
builder.Services.AddScoped<IDomainRepository, DomainRepository>();
builder.Services.AddScoped<IAgentRepository, AgentRepository>();
builder.Services.AddScoped<DomainService>();
builder.Services.AddScoped<AgentService>();
builder.Services.AddScoped<MailService>();
builder.Services.AddScoped<CertificateService>();
builder.Services.AddScoped<LogService>();
builder.Services.AddSingleton<LogStore>();
builder.Services.AddSingleton<DnsService>();
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient("Default");
builder.Services.AddHttpClient("IgnoreSSL").ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, _, _, _) => true });
var app = builder.Build();
// Tests override the connection string with isolated databases before this startup migration.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<AccountService>().BootstrapAsync(builder.Configuration["Authentication:Bootstrap:Email"], builder.Configuration["Authentication:Bootstrap:Password"]);
    await scope.ServiceProvider.GetRequiredService<SmtpSettingsService>().ImportAsync(builder.Configuration.GetSection("MailProperties").Get<MailProperties>());
}

app.MapDefaultEndpoints();
app.UseForwardedHeaders();
if (app.Environment.IsEnvironment("Testing"))
{
    app.UseDeveloperExceptionPage();
}

if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.Use(async (context, next) =>
{
    if (context.User.FindFirstValue("kind") == "human" && context.User.FindFirstValue("password_change") == "True")
    {
        var path = context.Request.Path;
        if (path != "/change-password" && path != "/account/password" && path != "/account/logout" && !path.StartsWithSegments("/_blazor") && !path.StartsWithSegments("/_framework") && path != "/Error")
        {
            if (AuthSetup.IsApi(path))
            {
                context.Response.StatusCode = 403;
            }
            else
            {
                context.Response.Redirect("/change-password");
            }

            return;
        }
    }

    await next();
});
app.UseAuthorization();
app.Use(async (context, next) =>
{
    if (context.User.FindFirstValue("kind") == "human" && !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method) && !HttpMethods.IsOptions(context.Request.Method) && !context.Request.Path.StartsWithSegments("/_blazor"))
    {
        try
        {
            await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context);
        }
        catch (AntiforgeryValidationException)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("Invalid form token. Reload the page and try again.");
            return;
        }
    }

    // Swagger is middleware, so endpoint authorization alone cannot protect it.
    if (context.Request.Path.StartsWithSegments("/api/swagger") && (await context.RequestServices.GetRequiredService<AccountService>().CurrentAsync(context.User)) is null)
    {
        context.Response.StatusCode = context.User.Identity?.IsAuthenticated == true ? 403 : 401;
        return;
    }

    await next();
});
app.UseAntiforgery();
app.UseSwagger(c => c.RouteTemplate = "api/swagger/{documentName}/swagger.json");
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/api/swagger/v1/swagger.json", "SSLTrack v1");
    c.RoutePrefix = "api/swagger";
});
app.MapControllers();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.UseHangfireDashboard("/hangfire", new DashboardOptions { Authorization = [new AuthorizationFilter()] });
app.Run();
