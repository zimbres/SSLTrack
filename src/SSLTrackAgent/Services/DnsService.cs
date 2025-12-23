namespace SSLTrackAgent.Services;

public class DnsService
{
    private readonly ILogger<DnsService> _logger;

    public DnsService(ILogger<DnsService> logger)
    {
        _logger = logger;
    }


    public async Task<IEnumerable<IPAddress>> GetIpAddress(string domainName)
    {
        try
        {
            var addresses = Dns.GetHostAddresses(domainName);
            return addresses;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error on DNS resolution for {domainName}", domainName);
            return null!;
        }
    }
}
