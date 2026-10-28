namespace SSLTrack.Services;

public class AgentService
{
    private readonly IAgentRepository _repository;
    private readonly IDbContextFactory<ApplicationDbContext> _factory;
    private readonly AccessGuard _access;
    public AgentService(IAgentRepository repository, AccessGuard access, IDbContextFactory<ApplicationDbContext> factory)
    {
        _repository = repository;
        _access = access;
        _factory = factory;
    }

    public async Task<IEnumerable<Agent>> GetAgents()
    {
        await _access.RequireHumanAsync();
        return await _repository.GetAll();
    }

    public async Task<IEnumerable<Agent>> GetAgent(string agentName)
    {
        await _access.RequireHumanAsync();
        return await _repository.Search(r => r.Name == agentName);
    }

    public async Task<IEnumerable<Agent>> GetAgent(int agentId)
    {
        await _access.RequireHumanAsync();
        return await _repository.Search(r => r.Id == agentId);
    }

    public async Task<Agent> AddAgent(string agentName)
    {
        await _access.RequireAdminAsync();
        var agent = new Agent
        {
            Name = agentName
        };
        if (await AgentExists())
        {
            agent.Id = 0;
        }

        var result = await _repository.Add(agent);
        if (result == 1)
        {
            return await _repository.GetOne();
        }

        return null!;
    }

    private async Task<bool> AgentExists()
    {
        var agent = await _repository.GetOne();
        if (agent is not null)
        {
            return false;
        }

        return true;
    }

    public async Task<bool> DeleteAgent(string agentName)
    {
        await _access.RequireAdminAsync();
        await using var db = await _factory.CreateDbContextAsync();
        var agent = await db.Agents.SingleOrDefaultAsync(x => x.Name == agentName);
        if (agent is null)
        {
            return false;
        }

        if (agent.Id < 2)
        {
            throw new InvalidOperationException("System agents cannot be removed.");
        }

        if (await db.ServiceAccounts.AnyAsync(x => x.AgentId == agent.Id))
        {
            throw new InvalidOperationException("Remove or reassign this agent's service accounts before deleting the agent.");
        }

        if (await db.Domains.AnyAsync(x => x.Agent == agent.Id))
        {
            throw new InvalidOperationException("Remove this agent's assigned domains before deleting the agent.");
        }

        db.Agents.Remove(agent);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException("The agent is still referenced. Remove its service accounts and assigned domains first.", ex);
        }

        return true;
    }
}
