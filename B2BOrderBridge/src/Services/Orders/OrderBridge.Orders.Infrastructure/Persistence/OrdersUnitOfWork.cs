using Shared.Application.Persistence;

namespace OrderBridge.Orders.Infrastructure.Persistence;

/// <summary>Saves tracked changes, including outbox entries explicitly staged by Application.</summary>
public sealed class OrdersUnitOfWork(OrdersDbContext db) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
}
