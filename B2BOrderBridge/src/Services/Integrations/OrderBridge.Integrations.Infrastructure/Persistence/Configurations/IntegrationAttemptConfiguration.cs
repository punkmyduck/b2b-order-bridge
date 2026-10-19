using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderBridge.Integrations.Application.Features.OrderIntegrations.Models;
using OrderBridge.Integrations.Domain.Models;

namespace OrderBridge.Integrations.Infrastructure.Persistence.Configurations;

public sealed class IntegrationAttemptConfiguration : IEntityTypeConfiguration<IntegrationAttempt>
{
    private const int OutcomeMaxLength = 32;
    public void Configure(EntityTypeBuilder<IntegrationAttempt> builder)
    {
        builder.ToTable("IntegrationAttempts", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_IntegrationAttempts_AttemptNumber_Positive",
                "\"AttemptNumber\" > 0");
            tableBuilder.HasCheckConstraint(
                "CK_IntegrationAttempts_ResponseStatusCode_Range",
                "\"ResponseStatusCode\" IS NULL OR \"ResponseStatusCode\" BETWEEN 100 AND 599");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.HasOne<OrderIntegration>()
            .WithMany()
            .HasForeignKey(x => x.OrderIntegrationId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.AttemptNumber)
            .IsRequired();

        builder.Property(x => x.Outcome)
            .HasConversion<string>()
            .HasMaxLength(OutcomeMaxLength)
            .IsRequired();

        builder.Property(x => x.ResponseStatusCode);
        builder.Property(x => x.ErrorCode)
            .HasMaxLength(IntegrationAttempt.ErrorCodeMaxLength);
        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(IntegrationAttempt.ErrorMessageMaxLength);
        builder.Property(x => x.TraceId)
            .HasMaxLength(IntegrationAttempt.TraceIdMaxLength);

        builder.Property(x => x.StartedAt)
            .IsRequired();
        builder.Property(x => x.CompletedAt);

        builder.HasIndex(x => new { x.OrderIntegrationId, x.AttemptNumber })
            .IsUnique();
        builder.HasIndex(x => new { x.OrderIntegrationId, x.StartedAt });
        builder.HasIndex(x => new { x.Outcome, x.StartedAt });
    }
}
