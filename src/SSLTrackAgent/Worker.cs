namespace SSLTrackAgent;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly WorkerService _workerService;
    private readonly Configurations _configurations;
    private readonly HttpClient _httpClient;

    public Worker(ILogger<Worker> logger, IConfiguration configuration, WorkerService workerService, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _workerService = workerService;
        _configurations = configuration.GetSection("Configurations").Get<Configurations>();
        _httpClientFactory = httpClientFactory;
        _httpClient = _httpClientFactory.CreateClient("Default");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogWarning("App version: {version}", Assembly.GetExecutingAssembly().GetName().Version?.ToString());

        var lastProcessTime = DateTimeOffset.MinValue;
        var lastHeartbeatTime = DateTimeOffset.MinValue;

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.Now;

            if (now - lastHeartbeatTime >= TimeSpan.FromSeconds(60))
            {
                try
                {
                    await _httpClient.SendAsync(new HttpRequestMessage(
                        HttpMethod.Head, $"{_configurations.SSLTrackApiAddress}{_configurations.PingEndpoint}/{_configurations.AgentId}"), stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Heartbeat failed: {message}", ex.Message);
                }

                lastHeartbeatTime = now;
            }

            if (now - lastProcessTime >= TimeSpan.FromSeconds(_configurations.Delay))
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    await _workerService.ProcessDomains();
                    _logger.LogInformation("Worker ran at: {time}", now);
                }

                lastProcessTime = now;
            }
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
}
