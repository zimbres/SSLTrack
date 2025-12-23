namespace SSLTrack.Repository;

public class DomainRepository : Repository<Domain>, IDomainRepository
{
    public DomainRepository(IDbContextFactory<ApplicationDbContext> db) : base(db)
    {
    }
}
