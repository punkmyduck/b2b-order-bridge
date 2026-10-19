using Shared.Domain;

namespace Shared.Application.Persistence;

/// <summary>Stages aggregate changes; implementations must not commit implicitly.</summary>
public interface IRepository<TEntity, in TId> : IReadRepository<TEntity, TId>
    where TEntity : Entity<TId>, IAggregateRoot
    where TId : notnull
{
    void Add(TEntity entity);
    void Update(TEntity entity);
    void Remove(TEntity entity);
}
