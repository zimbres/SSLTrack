namespace SSLTrack.Services;

public class HangfireService : IHostedService
{
    private readonly DomainService _domainService;
    private readonly Configurations _configurations;
    private readonly LogService _logService;

    public HangfireService(IServiceScopeFactory factory, IConfiguration configuration, LogService logService)
    {
        _domainService = factory.CreateScope().ServiceProvider.GetRequiredService<DomainService>();
        _configurations = configuration.GetSection("Configurations").Get<Configurations>()!;
        _logService = logService;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        RecurringJob.AddOrUpdate("Update All Domains", () => _domainService.UpdateAllDomains(), _configurations.UpdateCron);
        RecurringJob.AddOrUpdate("Alert Expiring Domains", () => _domainService.AlertExpiringDomains(), _configurations.AlertCron);
        RecurringJob.AddOrUpdate("Clear Logs", () => _logService.ClearLogs(), _configurations.ClearLogsCron);
        await Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
