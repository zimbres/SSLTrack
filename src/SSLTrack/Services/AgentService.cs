namespace SSLTrack.Services;

public class AgentService
{
    private readonly IAgentRepository _repository;

    public AgentService(IAgentRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<Agent>> GetAgents()
    {
        return await _repository.GetAll();
    }

    public async Task<IEnumerable<Agent>> GetAgent(string agentName)
    {
        return await _repository.Search(r => r.Name == agentName);
    }

    public async Task<IEnumerable<Agent>> GetAgent(int agentId)
    {
        return await _repository.Search(r => r.Id == agentId);
    }

    public async Task<Agent> AddAgent(string agentName)
    {
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
            return false;
        return true;
    }

    public async Task<bool> DeleteAgent(string agentName)
    {
        var agent = await _repository.Search(r => r.Name == agentName);
        if (!agent.Any())
        {
            return false;
        }
        await _repository.Remove(agent.First().Id);
        return true;
    }
}
