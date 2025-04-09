namespace SSLTrack.Extensions;

public static class Validators
{
    public static bool DomainExists(IEnumerable<Domain> domainDb, string domain, int port, int agentId)
    {
        if (!domainDb.Any())
        {
            return false;
        }
        if (domainDb.Where(d => d.DomainName == domain).Where(d => d.Port == port).Where(d => d.Agent == agentId).Any())
        {
            return true;
        }
        return false;
    }

    public static bool AgentExists(IEnumerable<Agent> agentDb, string agentName)
    {
        if (!agentDb.Any())
        {
            return false;
        }
        if (agentDb.First().Name == agentName)
        {
            return true;
        }
        return false;
    }
}
