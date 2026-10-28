namespace SSLTrack.Services;

public sealed class HangfireService(IConfiguration configuration) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var config = configuration.GetSection("Configurations").Get<Configurations>()!;
        RecurringJob.AddOrUpdate<DomainService>("Update All Domains", service => service.UpdateAllDomains(), config.UpdateCron);
        RecurringJob.AddOrUpdate<DomainService>("Alert Expiring Domains", service => service.AlertExpiringDomains(), config.AlertCron);
        RecurringJob.AddOrUpdate<LogService>("Clear Logs", service => service.ClearLogs(), config.ClearLogsCron);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
