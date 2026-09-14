using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderBridge.Domain.Models;
using OrderBridge.Domain.ValueObjects;

namespace OrderBridge.Infrastructure.Persistence.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Ignore(x => x.DomainEvents);
        builder.Property(x => x.SourceSystem).HasMaxLength(Order.SourceSystemMaxLength).IsRequired();
        builder.Property(x => x.ExternalOrderId).HasMaxLength(Order.ExternalOrderIdMaxLength).IsRequired();
        builder.HasIndex(x => new { x.SourceSystem, x.ExternalOrderId }).IsUnique();
        builder.Property(x => x.Currency).HasMaxLength(CurrencyCodes.CodeLength).IsRequired();
        builder.Property(x => x.TotalAmount).HasPrecision(31, 2);
        builder.Property(x => x.OrderStatus).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.PaymentStatus).HasConversion<string>().HasMaxLength(32);
        builder.Property<uint>("Version").IsRowVersion();

        builder.OwnsOne(x => x.Customer, customer =>
        {
            customer.Property(x => x.CompanyName).HasColumnName("CustomerCompanyName")
                .HasMaxLength(CustomerSnapshot.CompanyNameMaxLength).IsRequired();
            customer.Property(x => x.TaxId).HasColumnName("CustomerTaxId")
                .HasMaxLength(CustomerSnapshot.TaxIdMaxLength).IsRequired();
            customer.Property(x => x.ContactEmail).HasColumnName("CustomerContactEmail")
                .HasMaxLength(CustomerSnapshot.ContactEmailMaxLength).IsRequired();
        });
        builder.Navigation(x => x.Customer).IsRequired();

        builder.HasMany(x => x.Lines).WithOne().HasForeignKey("OrderId")
            .IsRequired().OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Lines).HasField("_lines")
            .UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
    }
}
