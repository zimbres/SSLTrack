var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Services.Configure<HttpClientConfiguration>(builder.Configuration.GetSection(nameof(HttpClientConfiguration)));
builder.Services.AddConfiguredHttpClient();
builder.Services.AddSingleton<AgentService>();
builder.Services.AddSingleton<WorkerService>();
builder.Services.AddSingleton<CertificateService>();
builder.Services.AddSingleton<LogService>();
builder.Services.AddSingleton<DnsService>();
builder.Services.AddSingleton<AuthService>();
builder.Services.AddHostedService<Worker>().Configure<HostOptions>(options =>
{
    options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
});

var host = builder.Build();
host.Run();
