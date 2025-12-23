namespace SSLTrack.Interfaces;

public interface IRepository<TEntity> where TEntity : Entity
{
    Task<int> Add(TEntity entity);
    Task<TEntity> GetById(int id);
    Task<TEntity> GetOne();
    Task<List<TEntity>> GetAll();
    Task Update(TEntity entity);
    Task Remove(int id);
    Task<IEnumerable<TEntity>> Search(Expression<Func<TEntity, bool>> predicate);
}