using Microsoft.EntityFrameworkCore;

namespace Shared.Messaging.Persistence;

public static class MessagingModel
{
    public static void AddOutbox(this ModelBuilder model)
    {
        var b = model.Entity<OutboxMessage>();

        b.ToTable("OutboxMessages");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.EventType).HasMaxLength(128).IsRequired();
        b.Property(x => x.Topic).HasMaxLength(249).IsRequired();
        b.Property(x => x.OrderingKey).HasMaxLength(512).IsRequired();
        b.Property(x => x.IdempotencyKey).HasMaxLength(512).IsRequired();
        b.Property(x => x.Payload).HasColumnType("jsonb").IsRequired();
        b.Property(x => x.LastError).HasMaxLength(2048);
        b.HasIndex(x => new { x.NextAttemptAt, x.OccurredAt }).HasFilter("\"PublishedAt\" IS NULL");
    }

    public static void AddInbox(this ModelBuilder model)
    {
        var b = model.Entity<InboxMessage>();

        b.ToTable("InboxMessages");
        b.HasKey(x => new { x.Consumer, x.EventId });
        b.Property(x => x.Consumer).HasMaxLength(128);
        b.Property(x => x.Source).HasMaxLength(128).IsRequired();
        b.Property(x => x.EventType).HasMaxLength(128).IsRequired();
        b.Property(x => x.IdempotencyKey).HasMaxLength(512).IsRequired();
        b.Property(x => x.OrderingKey).HasMaxLength(512).IsRequired();
        b.Property(x => x.Payload).HasColumnType("jsonb");
        b.HasIndex(x => new { x.Consumer, x.Source, x.EventType, x.IdempotencyKey }).IsUnique();
    }
}
