﻿namespace SSLTrackAgent.Services;

public class AgentService
{
    private readonly ILogger<AgentService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly Configurations _configurations;
    private readonly LogService _logService;
    private HttpClient _httpClient;

    public AgentService(ILogger<AgentService> logger, HttpClient httpClient, IHttpClientFactory httpClientFactory, IConfiguration configuration, LogService logService)
    {
        _logger = logger;
        _httpClient = httpClient;
        _httpClientFactory = httpClientFactory;
        _configurations = configuration.GetSection("Configurations").Get<Configurations>();
        _logService = logService;
        _httpClient = _httpClientFactory.CreateClient("Default");
        if (!string.IsNullOrEmpty(_configurations.Username) && !string.IsNullOrEmpty(_configurations.Password))
        {
            _httpClient.ApplyBasicAuth(_configurations.Username, _configurations.Password);
        }
    }

    public async Task<List<Domain>> GetDomains()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };


        try
        {
            var results = await _httpClient.GetFromJsonAsync<List<Domain>>($"{_configurations.SSLTrackApiAddress}{_configurations.GetDomainsEndpoint}{_configurations.AgentId}", options);
            return results;
        }
        catch (HttpRequestException ex)
        {
            if (ex.StatusCode == HttpStatusCode.NotFound)
            {
                await _logService.PushLog($"Agent Id '{_configurations.AgentId}' not found", "");
            }
            _logger.LogError(ex, "Error getting domains");
            return null;
        }
        catch (Exception ex)
        {
            await _logService.PushLog(ex.Message, "");
            _logger.LogError(ex, "Error getting domain information");
            return null;
        }
    }

    public async Task UpdateDomain(Domain payload)
    {
        try
        {
            await _httpClient.PutAsJsonAsync($"{_configurations.SSLTrackApiAddress}{_configurations.UpdateDomainEndpoint}{payload.DomainName}", payload);
        }
        catch (Exception ex)
        {
            await _logService.PushLog(ex.Message, payload.DomainName);
            _logger.LogError(ex, "Error updating domain information");
        }
    }
}
