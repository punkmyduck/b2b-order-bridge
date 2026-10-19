using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderBridge.Orders.Infrastructure.Messaging;
namespace OrderBridge.Orders.Infrastructure.Persistence.Configurations;
public sealed class OutboxConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> b)
    {
        b.ToTable("OutboxMessages"); b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.EventType).HasMaxLength(128).IsRequired();
        b.Property(x => x.Payload).HasColumnType("jsonb").IsRequired();
        b.Property(x => x.LastError).HasMaxLength(2048);
        b.HasIndex(x => new { x.NextAttemptAt, x.OccurredAt }).HasFilter("\"PublishedAt\" IS NULL");
    }
}
