namespace SSLTrackAgent.Services;

public class AuthService
{
    private readonly ILogger<AuthService> _logger;
    private readonly Configurations _configurations;
    private readonly IHttpClientFactory _httpClientFactory;
    private string _accessToken;
    private DateTime _tokenExpiry = DateTime.MinValue;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly HttpClient _httpClient;

    public AuthService(ILogger<AuthService> logger, IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _configurations = configuration.GetSection("Configurations").Get<Configurations>();
        _httpClientFactory = httpClientFactory;
        _httpClient = _httpClientFactory.CreateClient("Default");
    }

    public async Task<AuthenticationHeaderValue> GetAuthHeaderAsync()
    {
        if (string.IsNullOrWhiteSpace(_configurations.AuthType))
        {
            return null;
        }

        if (string.Equals(_configurations.AuthType, "Basic", StringComparison.OrdinalIgnoreCase))
        {
            var byteArray = Encoding.ASCII.GetBytes($"{_configurations.Username}:{_configurations.Password}");
            return new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
        }

        if (string.Equals(_configurations.AuthType, "Token", StringComparison.OrdinalIgnoreCase))
        {
            return await GetBearerTokenAsync();
        }

        _logger.LogError("Unsupported AuthType {_configurations.AuthType}", _configurations.AuthType);
        return null;
    }

    private async Task<AuthenticationHeaderValue> GetBearerTokenAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiry)
            {
                return new AuthenticationHeaderValue("Bearer", _accessToken);
            }

            var collection = new List<KeyValuePair<string, string>>
            {
                new("grant_type", _configurations.GrantType),
                new("client_id", _configurations.ClientId),
                new("username", _configurations.Username),
                new("password", _configurations.Password),
                new("scope", _configurations.Scope)
            };

            var content = new FormUrlEncodedContent(collection);

            var response = await _httpClient.PostAsync(_configurations.AuthUrl, content);
            response.EnsureSuccessStatusCode();

            var token = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(token);

            if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AccessToken))
            {
                _logger.LogError("Token response invalid: {json}", token);
            }

            _accessToken = tokenResponse.AccessToken;
            _tokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 30);

            return new AuthenticationHeaderValue("Bearer", _accessToken);
        }
        finally
        {
            _lock.Release();
        }
    }
}
