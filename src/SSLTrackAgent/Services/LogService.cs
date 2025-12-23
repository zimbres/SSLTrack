namespace SSLTrackAgent.Services;

public class LogService
{
    private readonly ILogger<LogService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly Configurations _configurations;
    private HttpClient _httpClient;
    private readonly AuthService _authService;

    public LogService(ILogger<LogService> logger, HttpClient httpClient, IHttpClientFactory httpClientFactory, IConfiguration configuration, AuthService authService)
    {
        _logger = logger;
        _httpClient = httpClient;
        _httpClientFactory = httpClientFactory;
        _configurations = configuration.GetSection("Configurations").Get<Configurations>();
        _authService = authService;
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
            await _httpClient.ApplyAuthAsync(_authService);
            await _httpClient.PostAsJsonAsync($"{_configurations.SSLTrackApiAddress}{_configurations.LogEndpoint}", payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending logs");
        }
    }
}
