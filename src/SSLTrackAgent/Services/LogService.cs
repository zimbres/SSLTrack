namespace SSLTrackAgent.Services;

public class LogService
{
    private readonly ILogger<LogService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly Configurations _configurations;
    private HttpClient _httpClient;

    public LogService(ILogger<LogService> logger, HttpClient httpClient, IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _logger = logger;
        _httpClient = httpClient;
        _httpClientFactory = httpClientFactory;
        _configurations = configuration.GetSection("Configurations").Get<Configurations>();
        if (!string.IsNullOrEmpty(_configurations.Username) && !string.IsNullOrEmpty(_configurations.Password))
        {
            _httpClient.ApplyBasicAuth(_configurations.Username, _configurations.Password);
        }
    }

    public async Task PushLog(string message, string domain)
    {
        _httpClient = _httpClientFactory.CreateClient("Default");

        var payload = new Log
        {
            Agent = _configurations.AgentId,
            Domain = domain,
            DateTime = DateTime.Now,
            Message = message,
        };

        try
        {
            await _httpClient.PostAsJsonAsync($"{_configurations.SSLTrackApiAddress}{_configurations.LogEndpoint}", payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending logs");
        }
    }
}
