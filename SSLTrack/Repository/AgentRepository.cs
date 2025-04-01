namespace SSLTrack.Repository;

public class AgentRepository : Repository<Agent>, IAgentRepository
{
    public AgentRepository(IDbContextFactory<ApplicationDbContext> db) : base(db)
    {
    }
}
