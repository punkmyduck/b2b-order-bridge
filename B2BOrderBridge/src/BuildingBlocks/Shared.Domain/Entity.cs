namespace Shared.Domain;

public abstract class Entity<TId> where TId : notnull
{
    protected Entity(TId id) => Id = id;

    // For persistence materialization.
    protected Entity() { }

    public TId Id { get; protected set; } = default!;
}
