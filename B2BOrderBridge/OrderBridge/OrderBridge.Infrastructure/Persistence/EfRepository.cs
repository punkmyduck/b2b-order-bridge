using Microsoft.EntityFrameworkCore;
using Shared.Application.Persistence;
using Shared.Domain;

namespace OrderBridge.Infrastructure.Persistence;

/// <summary>Tracks aggregates in the scoped context. Save through IUnitOfWork.</summary>
public class EfRepository<TEntity, TId>(OrderBridgeDbContext context) : IRepository<TEntity, TId>
    where TEntity : Entity<TId>, IAggregateRoot
    where TId : notnull
{
    protected OrderBridgeDbContext Context { get; } = context;
    protected DbSet<TEntity> Entities => Context.Set<TEntity>();

    public virtual Task<TEntity?> GetByIdAsync(TId id, CancellationToken cancellationToken = default)
        => Entities.SingleOrDefaultAsync(entity => entity.Id.Equals(id), cancellationToken);

    public virtual Task<bool> ExistsAsync(TId id, CancellationToken cancellationToken = default)
        => Entities.AnyAsync(entity => entity.Id.Equals(id), cancellationToken);

    public virtual void Add(TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        Entities.Add(entity);
    }

    public virtual void Update(TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        // Shadow concurrency tokens are available only on a loaded, tracked aggregate.
        if (Context.Entry(entity).State == EntityState.Detached)
            throw new InvalidOperationException("Load the aggregate in this unit of work before updating it.");
        Context.ChangeTracker.DetectChanges();
    }

    public virtual void Remove(TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (Context.Entry(entity).State == EntityState.Detached)
            throw new InvalidOperationException("Load the aggregate in this unit of work before removing it.");
        Entities.Remove(entity);
    }
}
