namespace SSLTrack.Repository;

public abstract class Repository<TEntity> : IRepository<TEntity> where TEntity : Entity, new()
{
    protected readonly IDbContextFactory<ApplicationDbContext> DbContextFactory;

    protected Repository(IDbContextFactory<ApplicationDbContext> dbContextFactory)
    {
        DbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
    }

    public async Task<IEnumerable<TEntity>> Search(Expression<Func<TEntity, bool>> predicate)
    {
        using var context = DbContextFactory.CreateDbContext();
        return await context.Set<TEntity>().AsNoTracking().Where(predicate).ToListAsync();
    }

    public virtual async Task<TEntity> GetOne()
    {
        using var context = DbContextFactory.CreateDbContext();
        return await context.Set<TEntity>().AsNoTracking().OrderBy(o => o.Id).LastOrDefaultAsync();
    }

    public virtual async Task<TEntity> GetById(int id)
    {
        using var context = DbContextFactory.CreateDbContext();
        return await context.Set<TEntity>().FindAsync(id);
    }

    public virtual async Task<List<TEntity>> GetAll()
    {
        using var context = DbContextFactory.CreateDbContext();
        return await context.Set<TEntity>().AsNoTracking().ToListAsync();
    }

    public virtual async Task<int> Add(TEntity entity)
    {
        using var context = DbContextFactory.CreateDbContext();
        context.Set<TEntity>().Add(entity);
        return await context.SaveChangesAsync();
    }

    public virtual async Task Update(TEntity entity)
    {
        using var context = DbContextFactory.CreateDbContext();
        context.Set<TEntity>().Update(entity);
        await context.SaveChangesAsync();
    }

    public virtual async Task Remove(int id)
    {
        using var context = DbContextFactory.CreateDbContext();
        var entity = new TEntity { Id = id };
        context.Set<TEntity>().Attach(entity);
        context.Set<TEntity>().Remove(entity);
        await context.SaveChangesAsync();
    }
}
