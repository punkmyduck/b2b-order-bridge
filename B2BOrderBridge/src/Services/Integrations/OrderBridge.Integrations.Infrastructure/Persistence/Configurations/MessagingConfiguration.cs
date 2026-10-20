using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace OrderBridge.Integrations.Infrastructure.Persistence.Configurations;
public sealed class IntegrationOrderConfiguration : IEntityTypeConfiguration<IntegrationOrder>
{
    public void Configure(EntityTypeBuilder<IntegrationOrder> b)
    {
        b.ToTable("IntegrationOrders"); b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Payload).HasColumnType("jsonb").IsRequired();
    }
}
