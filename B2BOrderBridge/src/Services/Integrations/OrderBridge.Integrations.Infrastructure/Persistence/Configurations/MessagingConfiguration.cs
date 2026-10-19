using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderBridge.Integrations.Infrastructure.Messaging;
namespace OrderBridge.Integrations.Infrastructure.Persistence.Configurations;
public sealed class InboxConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> b)
    {
        b.ToTable("InboxMessages"); b.HasKey(x => new { x.Consumer, x.EventId });
        b.Property(x => x.Consumer).HasMaxLength(128);
    }
}
public sealed class IntegrationOrderConfiguration : IEntityTypeConfiguration<IntegrationOrder>
{
    public void Configure(EntityTypeBuilder<IntegrationOrder> b)
    {
        b.ToTable("IntegrationOrders"); b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Payload).HasColumnType("jsonb").IsRequired();
    }
}
