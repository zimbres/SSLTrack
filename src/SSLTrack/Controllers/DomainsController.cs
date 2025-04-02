namespace SSLTrack.Controllers;

[Route("api/[controller]")]
[ApiController]
public class DomainsController : ControllerBase
{
    private readonly DomainService _domainService;
    private readonly AgentService _agentService;

    public DomainsController(DomainService domainService, AgentService agentService)
    {
        _domainService = domainService;
        _agentService = agentService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Domain>>> GetDomains()
    {
        var domains = await _domainService.GetDomains();
        return Ok(domains);
    }


    [HttpGet("agents/{agent}")]
    public async Task<ActionResult<IEnumerable<Domain>>> GetDomainsByAgentId(int agent)
    {
        var agentDb = await _agentService.GetAgent(agent);
        if (!agentDb.Any())
        {
            return NotFound();
        }
        var domains = await _domainService.GetDomains(agent);
        return Ok(domains);
    }

    [HttpGet("{domainName}")]
    public async Task<ActionResult<Domain>> GetDomain(string domainName)
    {
        var domain = await _domainService.GetDomain(domainName);

        if (!domain.Any())
        {
            return NotFound();
        }
        return Ok(domain);
    }

    [HttpPost("update")]
    public async Task<IActionResult> UpdateAll()
    {
        await _domainService.UpdateAllDomains();
        return Ok();
    }

    [HttpPut("{domainName}")]
    public async Task<IActionResult> PutDomain(string domainName, Domain domain)
    {
        if (domainName != domain.DomainName)
        {
            return BadRequest();
        }

        var updatedDomain = await _domainService.UpdateDomain(domainName, domain);
        if (updatedDomain)
        {
            return NoContent();
        }
        return NotFound();
    }

    [HttpPost]
    public async Task<ActionResult<Domain>> PostDomain(string domain, int port, string? issuer = null, DateTime? expirationDate = null)
    {
        var result = await _domainService.AddDomain(domain, port, issuer, expirationDate);
        if (result is not null)
        {
            return Ok(result);
        }
        return StatusCode(500);
    }

    [HttpDelete("{domainName}")]
    public async Task<IActionResult> DeleteDomain(int domainId)
    {
        var result = await _domainService.DeleteDomain(domainId);
        if (result is false)
        {
            return NotFound();
        }
        return NoContent();
    }
}
