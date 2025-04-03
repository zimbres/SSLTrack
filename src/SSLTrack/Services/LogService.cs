namespace SSLTrack.Services;

public class LogService
{
    private readonly ILogger<LogService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly Configurations _configurations;
    private readonly AgentService _agentService;
    private HttpClient _httpClient;

    public LogService(ILogger<LogService> logger, IHttpClientFactory httpClientFactory, IConfiguration configuration, IServiceScopeFactory factory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _configurations = configuration.GetSection("Configurations").Get<Configurations>();
        _agentService = factory.CreateScope().ServiceProvider.GetRequiredService<AgentService>();
        _httpClient = _httpClientFactory.CreateClient("Default");
    }

    public async Task ClearLogs()
    {
        var message = new HttpRequestMessage(HttpMethod.Post, _configurations.ApiBaseAddress + _configurations.ClearLogsEndpoint);

        try
        {
            await _httpClient.SendAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning logs");
        }
    }

    public async Task PushLog(string message, string domain)
    {
        var payload = new Log
        {
            Agent = 0,
            Domain = domain,
            DateTime = DateTime.Now,
            Message = message,
        };

        try
        {
            await _httpClient.PostAsJsonAsync(_configurations.ApiBaseAddress + _configurations.LogsEndpoint, payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending logs");
        }
    }

    public async Task<IEnumerable<LogEntryDto>> LogEntryDtos(IEnumerable<LogEntry> logs)
    {
        var agents = await _agentService.GetAgents();

        var agentDict = agents.ToDictionary(a => a.Id, a => a.Name ?? "Unknown");

        var logsEntrie = logs.Select((entry, index) => new LogEntryDto
        {
            Id = entry.Id,
            Agent = agentDict.TryGetValue(entry.Agent, out var agentName) ? agentName : "Unknown",
            Domain = entry.Domain,
            DateTime = entry.DateTime,
            Message = entry.Message
        }).ToList();

        return logsEntrie;
    }

    public async Task<IEnumerable<LogEntryDto>> LogEntry(string logs)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var dictionary = JsonSerializer.Deserialize<Dictionary<string, LogEntry>>(logs, options);

        var logEntries = new List<LogEntry>();
        if (dictionary != null)
        {
            foreach (var kvp in dictionary)
            {
                kvp.Value.Id = int.Parse(kvp.Key);
                logEntries.Add(kvp.Value);
            }
        }
        var logEntriesDto = await LogEntryDtos(logEntries);
        return logEntriesDto;
    }
}
