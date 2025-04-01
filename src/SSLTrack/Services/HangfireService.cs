namespace SSLTrack.Services;

public class HangfireService : IHostedService
{
    private readonly DomainService _domainService;
    private readonly Configurations _configurations;

    public HangfireService(IServiceScopeFactory factory, IConfiguration configuration)
    {
        _domainService = factory.CreateScope().ServiceProvider.GetRequiredService<DomainService>();
        _configurations = configuration.GetSection("Configurations").Get<Configurations>()!;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        RecurringJob.AddOrUpdate("Update All Domains", () => _domainService.UpdateAllDomains(), _configurations.UpdateCron);
        RecurringJob.AddOrUpdate("Alert Expiring Domains", () => _domainService.AlertExpiringDomains(), _configurations.AlertCron);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
