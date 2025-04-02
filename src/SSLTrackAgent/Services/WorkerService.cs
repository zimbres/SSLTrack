namespace SSLTrackAgent.Services;

public class WorkerService
{
    private readonly ILogger<AgentService> _logger;
    private readonly AgentService _agentService;
    private readonly CertificateService _certificateDownloader;

    public WorkerService(ILogger<AgentService> logger, AgentService agentService, CertificateService certificateDownloader)
    {
        _logger = logger;
        _agentService = agentService;
        _certificateDownloader = certificateDownloader;
    }

    public async Task ProcessDomains()
    {
        var domains = await _agentService.GetDomains();

        if (domains is not null)
        {
            foreach (var domain in domains)
            {
                var certificate = await _certificateDownloader.GetCertificateAsync(domain.DomainName, domain.Port);
                if (certificate is not null)
                {
                    var payload = new Domain
                    {
                        DomainName = domain.DomainName,
                        Port = domain.Port,
                        CertCN = certificate.Subject.FormateCN(),
                        Issuer = certificate.Issuer.FormateIssuer(),
                        ExpiryDate = certificate.NotAfter,
                        LastChecked = DateTime.Now,
                        Agent = domain.Agent,
                        UserId = domain.UserId,
                        Silenced = domain.Silenced,
                        Id = domain.Id,
                    };
                    await _agentService.UpdateDomain(payload);
                }
            }
        }
    }
}
