﻿var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddHangfire(options =>
{
    options.UseInMemoryStorage();
});
builder.Services.AddHangfireServer();
builder.Services.AddMudServices();
builder.Services.AddControllers();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "SSLTrack", Version = "v1" });
});
builder.Services.AddDataProtection().PersistKeysToDbContext<ApplicationDbContext>();
builder.Services.AddDbContextFactory<ApplicationDbContext>();
builder.Services.AddScoped<IDomainRepository, DomainRepository>();
builder.Services.AddHostedService<HangfireService>();
builder.Services.AddScoped<DomainService>();
builder.Services.AddTransient(provider => new SmtpClient());

builder.Services.AddScoped<MailService>();
builder.Services.AddSingleton<CertificateService>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ThemeService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseMiddleware<ThemeMiddleware>();
app.MapControllers();
app.UseStaticFiles();
app.UseAntiforgery();
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "SSLTrack v1");
});
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.UseHangfireDashboard("/hangfire");
app.Run();
