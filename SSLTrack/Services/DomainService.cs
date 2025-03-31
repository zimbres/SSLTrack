namespace SSLTrack.Services;

public class DomainService
{
    private readonly IDomainRepository _repository;
    private readonly MailService _mailService;
    private readonly Configurations _configurations;
    private readonly CertificateService _certificateDownloader;

    public DomainService(IDomainRepository repository, MailService mailService, IConfiguration configuration,
        CertificateService certificateDownloader)
    {
        _repository = repository;
        _mailService = mailService;
        _configurations = configuration.GetSection("Configurations").Get<Configurations>()!;
        _certificateDownloader = certificateDownloader;
    }

    public async Task<IEnumerable<Domain>> GetDomains()
    {
        return await _repository.GetAll();
    }

    public async Task<IEnumerable<Domain>> GetDomain(string domainName)
    {
        return await _repository.Search(r => r.DomainName == domainName);
    }

    public async Task<IEnumerable<Domain>> GetDomains(int agent)
    {
        return await _repository.Search(r => r.Agent == agent);
    }

    public async Task<Domain> AddDomain(string domainName, int port, string? issuer = null, DateTime? expirationDate = null)
    {
        if (issuer is not null)
        {
            var domain = new Domain
            {
                DomainName = domainName,
                Port = port,
                CertCN = domainName,
                Issuer = issuer,
                ExpiryDate = expirationDate ?? DateTime.Today,
                LastChecked = expirationDate ?? DateTime.Today,
                Agent = 99
            };
            var result = await _repository.Add(domain);
            if (result == 1)
            {
                return await _repository.GetOne();
            }
        }
        var certificate = await _certificateDownloader.GetCertificateAsync(domainName, port);
        if (certificate is not null)
        {
            var domain = new Domain
            {
                DomainName = domainName,
                Port = port,
                CertCN = certificate.Subject.FormateCN(),
                Issuer = certificate.Issuer.FormateIssuer(),
                ExpiryDate = certificate.NotAfter,
                LastChecked = DateTime.Now,
            };
            var result = await _repository.Add(domain);
            if (result == 1)
            {
                return await _repository.GetOne();
            }
        }
        return null!;
    }

    public async Task<bool> UpdateDomain(string domainName, Domain domain)
    {
        try
        {
            await _repository.Update(domain);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await DomainExists(domainName))
            {
                return false;
            }
            else
            {
                throw;
            }
        }
        return true;
    }

    private async Task<bool> DomainExists(string domainName)
    {
        var domain = await _repository.Search(e => e.DomainName == domainName);
        return domain.Any();
    }

    public async Task<bool> DeleteDomain(string domainName)
    {
        var domain = await _repository.Search(r => r.DomainName == domainName);
        if (!domain.Any())
        {
            return false;
        }
        await _repository.Remove(domain.First().Id);
        return true;
    }

    public async Task UpdateAllDomains()
    {
        var domains = await _repository.GetAll();
        foreach (var domain in domains)
        {
            if (domain.Agent != 0)
                continue;

            var certificate = await _certificateDownloader.GetCertificateAsync(domain.DomainName, domain.Port);
            if (certificate is not null)
            {
                var updated = new Domain
                {
                    Id = domain.Id,
                    DomainName = domain.DomainName,
                    Port = domain.Port,
                    CertCN = certificate.Subject.FormateCN(),
                    Issuer = certificate.Issuer.FormateIssuer(),
                    ExpiryDate = certificate.NotAfter,
                    LastChecked = DateTime.Now,
                    UserId = domain.UserId,
                    Agent = domain.Agent,
                    Silenced = domain.Silenced != false && Expiration.GetExpirationStatus(certificate.NotAfter, _configurations.DaysToExpiration)
                };
                await _repository.Update(updated);
            }
        }
    }

    public async Task AlertExpiringDomains()
    {
        var domains = await _repository.GetAll();
        foreach (var domain in domains)
        {
            if (!_configurations.AlertsEnabled)
                continue;

            if (domain.Silenced)
                continue;

            var now = DateTime.Now;

            if (domain.ExpiryDate < now)
            {
                _mailService.Subject = $"Certificate for domain {domain.DomainName.ToUpper()} has expired!";
                _mailService.Body = $"Certificate for domain {domain.DomainName.ToUpper()} on port {domain.Port} expired on {domain.ExpiryDate}. Immediate action required!";
            }
            else if (domain.ExpiryDate < now.AddDays(_configurations.DaysToExpiration))
            {
                _mailService.Subject = $"Certificate for domain {domain.DomainName.ToUpper()} is close to expiration date!";
                _mailService.Body = $"Certificate for domain {domain.DomainName.ToUpper()} on port {domain.Port} will expire on {domain.ExpiryDate}. Please renew it soon.";
            }
            else
            {
                continue;
            }

            _mailService.SendMail();
        }
    }
}
