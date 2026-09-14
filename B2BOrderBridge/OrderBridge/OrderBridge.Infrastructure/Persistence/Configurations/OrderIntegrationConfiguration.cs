using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderBridge.Domain.Models;

namespace OrderBridge.Infrastructure.Persistence.Configurations;

public sealed class OrderIntegrationConfiguration : IEntityTypeConfiguration<OrderIntegration>
{
    public void Configure(EntityTypeBuilder<OrderIntegration> builder)
    {
        builder.ToTable("OrderIntegrations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Ignore(x => x.DomainEvents);
        builder.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.IntegrationTarget).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.IntegrationOperation).HasConversion<string>().HasMaxLength(64);
        builder.Property(x => x.IntegrationStatus).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(OrderIntegration.IdempotencyKeyMaxLength).IsRequired();
        builder.Property(x => x.ExternalEntityId).HasMaxLength(OrderIntegration.ExternalEntityIdMaxLength);
        builder.Property(x => x.LastErrorCode).HasMaxLength(OrderIntegration.LastErrorCodeMaxLength);
        builder.Property(x => x.LastErrorMessage).HasMaxLength(OrderIntegration.LastErrorMessageMaxLength);
        builder.HasIndex(x => new { x.OrderId, x.IntegrationTarget, x.IntegrationOperation }).IsUnique();
        builder.HasIndex(x => new { x.IntegrationTarget, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => new { x.IntegrationStatus, x.NextAttemptAt });
        builder.Property<uint>("Version").IsRowVersion();
    }
}
