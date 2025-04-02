var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddHttpClient("Default");
builder.Services.AddHttpClient("IgnoreSSL")
.ConfigurePrimaryHttpMessageHandler(() =>
{
    return new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = (m, c, ch, e) => true
    };
});
builder.Services.AddSingleton<AgentService>();
builder.Services.AddSingleton<WorkerService>();
builder.Services.AddSingleton<CertificateService>();
builder.Services.AddSingleton<LogService>();
builder.Services.AddHostedService<Worker>().Configure<HostOptions>(options =>
{
    options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
});

var host = builder.Build();
host.Run();
