namespace SSLTrack.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Policy = "AgentOrHuman")]
public class DomainsController(DomainService domains, IDbContextFactory<ApplicationDbContext> factory) : ControllerBase
{
    [HttpGet, Authorize(Policy = "Human")]
    public async Task<IActionResult> GetDomains() => Ok(await domains.GetDomains());
    [HttpGet("agents/{agent}")]
    public async Task<IActionResult> GetDomainsByAgentId(int agent)
    {
        if (IsAgent && AgentId != agent)
        {
            return Forbid();
        }

        await using var db = await factory.CreateDbContextAsync();
        if (!await db.Agents.AnyAsync(x => x.Id == agent))
        {
            return NotFound();
        }

        return Ok(await db.Domains.AsNoTracking().Where(x => x.Agent == agent).ToListAsync());
    }

    [HttpGet("{domainName}"), Authorize(Policy = "Human")]
    public async Task<IActionResult> GetDomain(string domainName)
    {
        var result = await domains.GetDomain(domainName);
        return result.Any() ? Ok(result) : NotFound();
    }

    [HttpPost("update"), Authorize(Policy = "Admin")]
    public async Task<IActionResult> UpdateAll()
    {
        await domains.UpdateAllDomainsForUser();
        return Ok();
    }

    [HttpPut("{domainName}")]
    public async Task<IActionResult> PutDomain(string domainName, Domain report)
    {
        if (domainName != report.DomainName)
        {
            return BadRequest();
        }

        if (!IsAgent)
        {
            if (!User.IsInRole("Admin"))
            {
                return Forbid();
            }

            return await domains.UpdateDomain(domainName, report) ? NoContent() : NotFound();
        }

        await using var db = await factory.CreateDbContextAsync();
        // Match the assigned agent and original port. Never trust IDs or assignment in a report.
        var matches = await db.Domains.Where(x => x.DomainName == domainName && x.Agent == AgentId).ToListAsync();
        if (matches.Count == 0)
        {
            return Forbid();
        }

        var exact = matches.Where(x => x.Port == report.Port).ToList();
        var domain = matches.Count == 1 ? matches[0] : exact.Count == 1 ? exact[0] : null;
        if (domain is null)
        {
            return Conflict("The report does not identify one assigned domain and port.");
        }

        domain.CertCN = report.CertCN;
        domain.Issuer = report.Issuer;
        domain.ExpiryDate = report.ExpiryDate;
        domain.LastChecked = report.LastChecked;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost, Authorize(Policy = "Admin")]
    public async Task<IActionResult> PostDomain(string domain, int port, string? issuer = null, DateTime? expirationDate = null)
    {
        var result = await domains.AddDomain(domain, port, issuer, expirationDate);
        return result is null ? StatusCode(500) : Ok(result);
    }

    [HttpDelete("{domainId:int}"), Authorize(Policy = "Admin")]
    public async Task<IActionResult> DeleteDomain(int domainId) => await domains.DeleteDomain(domainId) ? NoContent() : NotFound();
    private bool IsAgent => User.FindFirstValue("kind") == "agent";
    private int AgentId => int.Parse(User.FindFirstValue("agent_id")!);
}
