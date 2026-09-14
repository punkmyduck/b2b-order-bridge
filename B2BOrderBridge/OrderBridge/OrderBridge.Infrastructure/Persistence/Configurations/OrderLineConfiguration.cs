using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderBridge.Domain.Models;

namespace OrderBridge.Infrastructure.Persistence.Configurations;

public sealed class OrderLineConfiguration : IEntityTypeConfiguration<OrderLine>
{
    public void Configure(EntityTypeBuilder<OrderLine> builder)
    {
        builder.ToTable("OrderLines");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Sku).HasMaxLength(OrderLine.SkuMaxLength).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(OrderLine.NameMaxLength).IsRequired();
        // No imposed scale: the domain currently accepts arbitrary decimal quantities.
        builder.Property(x => x.Quantity).HasColumnType("numeric");
        builder.Property(x => x.UnitPrice).HasPrecision(31, 2);
        builder.Property(x => x.LineTotal).HasPrecision(31, 2);
    }
}
