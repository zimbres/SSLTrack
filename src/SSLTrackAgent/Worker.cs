namespace SSLTrackAgent;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly WorkerService _workerService;
    private readonly Configurations _configurations;

    public Worker(ILogger<Worker> logger, IConfiguration configuration, WorkerService workerService)
    {
        _logger = logger;
        _workerService = workerService;
        _configurations = configuration.GetSection("Configurations").Get<Configurations>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogWarning("App version: {version}", Assembly.GetExecutingAssembly().GetName().Version.ToString());

        while (!stoppingToken.IsCancellationRequested)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                await _workerService.ProcessDomains();
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            }
            await Task.Delay(_configurations.Delay * 1000, stoppingToken);
        }
    }
}
