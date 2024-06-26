﻿namespace SSLTrack.Services;

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

    public async Task<Domain> AddDomain(string domainName, int port)
    {
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
            if (domain.ExpiryDate < DateTime.Now.AddDays(_configurations.DaysToExpiration) && _configurations.AlertsEnabled)
            {
                _mailService.Subject = $"Certificate for domain {domain.DomainName.ToUpper()} is close to expiration date!";
                _mailService.Body = $"Certificate for domain {domain.DomainName.ToUpper()} on port {domain.Port} will expire on {domain.ExpiryDate}";
                _mailService.SendMail();
            }
        }
    }
}
